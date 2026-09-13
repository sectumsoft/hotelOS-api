using HotelManagement.Application.Features.Bookings.Commands;
using HotelManagement.Domain.Entities;
using HotelManagement.Domain.Enums;

namespace HotelManagement.Tests;

/// <summary>
/// Regression coverage for a real bug: the check-in form collects no phone
/// number, so every guest arrived at CheckInCommandHandler with Phone == "".
/// The handler used to look up/dedupe EVERY check-in guest (including the
/// primary) by phone, which — because they all share the same blank phone —
/// either spawned a duplicate row for the primary guest (their real ID proof
/// landed on the duplicate, not the profile Guest 360 actually reads) or
/// collided two unrelated companions from different bookings onto one row.
/// </summary>
public class CheckInCommandTests
{
    private static (Infrastructure.Data.ApplicationDbContext db, Booking booking, Guest guest) SeedConfirmedBooking(string guestPhone = "+910000000001")
    {
        var db = TestContext.NewDb();
        var room = new Room
        {
            Id = Guid.NewGuid(),
            TenantId = TestContext.TenantId,
            RoomNumber = "301",
            RoomType = "Suite",
            PricePerNight = 2000m,
            Status = RoomStatus.Available,
        };
        var guest = new Guest
        {
            Id = Guid.NewGuid(),
            TenantId = TestContext.TenantId,
            Name = "Arjun",
            Phone = guestPhone,
            TotalStays = 1,
        };
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            TenantId = TestContext.TenantId,
            RoomId = room.Id,
            GuestId = guest.Id,
            BookingNumber = "BK-0001",
            GuestName = guest.Name,
            GuestPhone = guestPhone,
            CheckInDate = DateTime.SpecifyKind(new DateTime(2026, 6, 10), DateTimeKind.Utc),
            CheckOutDate = DateTime.SpecifyKind(new DateTime(2026, 6, 12), DateTimeKind.Utc),
            TotalNights = 2,
            TotalAmount = 4000m,
            BalanceAmount = 4000m,
            Status = BookingStatus.Confirmed,
        };
        db.Rooms.Add(room);
        db.Guests.Add(guest);
        db.Bookings.Add(booking);
        db.SaveChanges();
        return (db, booking, guest);
    }

    private static CheckInCommandHandler NewHandler(Infrastructure.Data.ApplicationDbContext db)
        => new(db, new TestContext.FakeTenantService(), new TestContext.NullNotificationRecorder());

    [Fact]
    public async Task Primary_guests_id_proof_lands_on_the_existing_guest_record_not_a_duplicate()
    {
        var (db, booking, guest) = SeedConfirmedBooking();
        using var _ = db;

        var ok = await NewHandler(db).Handle(new CheckInCommand(
            booking.Id, 4000m,
            new() { new CheckInGuestDto { Name = "Arjun", Phone = "", IdProofType = "Aadhar", IdProofNumber = "145555555555", IdProofUrl = "/uploads/idproofs/a.png" } }),
            default);

        Assert.True(ok);

        // No duplicate row — still just the one guest that was there before check-in.
        Assert.Single(db.Guests);

        var updated = db.Guests.Single(g => g.Id == guest.Id);
        Assert.Equal("145555555555", updated.IdProofNumber);
        Assert.Equal("/uploads/idproofs/a.png", updated.IdProofUrl);
    }

    [Fact]
    public async Task Check_in_does_not_blank_out_the_bookings_real_phone_and_address()
    {
        var (db, booking, _) = SeedConfirmedBooking(guestPhone: "+919876543210");
        using var _ = db;
        booking.GuestAddress = "221B Baker Street";
        db.SaveChanges();

        await NewHandler(db).Handle(new CheckInCommand(
            booking.Id, 0m,
            new() { new CheckInGuestDto { Name = "Arjun", Phone = "", Address = null, IdProofNumber = "145555555555" } }),
            default);

        var updated = db.Bookings.Single(b => b.Id == booking.Id);
        Assert.Equal("+919876543210", updated.GuestPhone);   // not wiped to ""
        Assert.Equal("221B Baker Street", updated.GuestAddress);
    }

    [Fact]
    public async Task Companions_get_their_own_record_and_dont_collide_across_bookings()
    {
        var (db1, booking1, _) = SeedConfirmedBooking();
        using var db = db1;

        await NewHandler(db).Handle(new CheckInCommand(
            booking1.Id, 0m,
            new()
            {
                new CheckInGuestDto { Name = "Arjun", Phone = "", IdProofNumber = "111" },
                new CheckInGuestDto { Name = "Draupati", Phone = "", IdProofNumber = "222", IdProofUrl = "/uploads/idproofs/draupati.png" },
            }),
            default);

        var companions = db.Guests.Where(g => g.BookingId == booking1.Id).ToList();
        Assert.Single(companions);
        Assert.Equal("Draupati", companions[0].Name);
        Assert.Equal("/uploads/idproofs/draupati.png", companions[0].IdProofUrl);

        // A second booking's companion, also with a blank phone, must not merge
        // into Draupati's row just because both have Phone == "".
        var room2 = new Room { Id = Guid.NewGuid(), TenantId = TestContext.TenantId, RoomNumber = "302", RoomType = "Suite", PricePerNight = 2000m };
        var guest2 = new Guest { Id = Guid.NewGuid(), TenantId = TestContext.TenantId, Name = "Karna", Phone = "+910000000099" };
        var booking2 = new Booking
        {
            Id = Guid.NewGuid(), TenantId = TestContext.TenantId, RoomId = room2.Id, GuestId = guest2.Id,
            BookingNumber = "BK-0002", GuestName = guest2.Name, GuestPhone = guest2.Phone,
            CheckInDate = DateTime.SpecifyKind(new DateTime(2026, 6, 10), DateTimeKind.Utc),
            CheckOutDate = DateTime.SpecifyKind(new DateTime(2026, 6, 12), DateTimeKind.Utc),
            TotalNights = 2, TotalAmount = 4000m, BalanceAmount = 4000m, Status = BookingStatus.Confirmed,
        };
        db.Rooms.Add(room2); db.Guests.Add(guest2); db.Bookings.Add(booking2);
        db.SaveChanges();

        await NewHandler(db).Handle(new CheckInCommand(
            booking2.Id, 0m,
            new()
            {
                new CheckInGuestDto { Name = "Karna", Phone = "", IdProofNumber = "333" },
                new CheckInGuestDto { Name = "Kunti", Phone = "", IdProofNumber = "444" },
            }),
            default);

        // Draupati's record is untouched by booking2's check-in.
        var draupatiAfter = db.Guests.Single(g => g.Name == "Draupati");
        Assert.Equal(booking1.Id, draupatiAfter.BookingId);
        Assert.Equal("222", draupatiAfter.IdProofNumber);

        var booking2Companions = db.Guests.Where(g => g.BookingId == booking2.Id).ToList();
        Assert.Single(booking2Companions);
        Assert.Equal("Kunti", booking2Companions[0].Name);
    }
}
