using HotelManagement.Application.Features.Bookings.Commands;
using HotelManagement.Domain.Entities;
using HotelManagement.Domain.Enums;

namespace HotelManagement.Tests;

public class BookingOverlapTests
{
    private static Room SeedRoom(Infrastructure.Data.ApplicationDbContext db)
    {
        var room = new Room
        {
            Id = Guid.NewGuid(),
            TenantId = TestContext.TenantId,
            RoomNumber = "101",
            RoomType = "Standard",
            PricePerNight = 1000m,
            Status = RoomStatus.Available,
        };
        db.Rooms.Add(room);
        db.SaveChanges();
        return room;
    }

    private static CreateBookingCommandHandler NewHandler(Infrastructure.Data.ApplicationDbContext db)
        => new(db, new TestContext.FakeTenantService(), new TestContext.NullNotificationRecorder());

    private static CreateBookingCommand Booking(Guid roomId, int checkInDay, int checkOutDay, string phone = "+910000000001")
        => new("Guest", phone, null, roomId,
               new DateTime(2026, 6, checkInDay), new DateTime(2026, 6, checkOutDay),
               NumberOfGuests: 1, AdvancePaid: false, AdvanceAmount: 0m);

    [Fact]
    public async Task Creates_a_booking_when_the_room_is_free()
    {
        using var db = TestContext.NewDb();
        var room = SeedRoom(db);

        var id = await NewHandler(db).Handle(Booking(room.Id, 10, 13), default);

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(3, db.Bookings.Single().TotalNights);
    }

    [Fact]
    public async Task Rejects_a_booking_that_overlaps_an_existing_one()
    {
        using var db = TestContext.NewDb();
        var room = SeedRoom(db);
        await NewHandler(db).Handle(Booking(room.Id, 10, 13), default);

        var ex = await Assert.ThrowsAsync<Exception>(
            () => NewHandler(db).Handle(Booking(room.Id, 12, 15, "+910000000002"), default));

        Assert.Contains("already booked", ex.Message);
    }

    [Fact]
    public async Task Allows_a_back_to_back_booking_that_starts_on_the_previous_checkout_day()
    {
        using var db = TestContext.NewDb();
        var room = SeedRoom(db);
        await NewHandler(db).Handle(Booking(room.Id, 10, 13), default);

        var id = await NewHandler(db).Handle(Booking(room.Id, 13, 16, "+910000000002"), default);

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(2, db.Bookings.Count());
    }

    [Fact]
    public async Task A_cancelled_booking_does_not_block_the_dates()
    {
        using var db = TestContext.NewDb();
        var room = SeedRoom(db);
        db.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            TenantId = TestContext.TenantId,
            RoomId = room.Id,
            BookingNumber = "BK-9999",
            GuestName = "Cancelled",
            GuestPhone = "+910000009999",
            CheckInDate = DateTime.SpecifyKind(new DateTime(2026, 6, 10), DateTimeKind.Utc),
            CheckOutDate = DateTime.SpecifyKind(new DateTime(2026, 6, 13), DateTimeKind.Utc),
            Status = BookingStatus.Cancelled,
        });
        db.SaveChanges();

        var id = await NewHandler(db).Handle(Booking(room.Id, 11, 14), default);

        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Rejects_a_zero_night_stay()
    {
        using var db = TestContext.NewDb();
        var room = SeedRoom(db);

        await Assert.ThrowsAsync<Exception>(
            () => NewHandler(db).Handle(Booking(room.Id, 10, 10), default));
    }
}
