using HotelManagement.Application.Features.Bookings.Commands;
using HotelManagement.Domain.Entities;
using HotelManagement.Domain.Enums;

namespace HotelManagement.Tests;

public class BillMathTests
{
    private static (Infrastructure.Data.ApplicationDbContext db, Booking booking) Seed(decimal pricePerNight = 1000m, int nights = 3, decimal advance = 500m)
    {
        var db = TestContext.NewDb();
        var room = new Room
        {
            Id = Guid.NewGuid(),
            TenantId = TestContext.TenantId,
            RoomNumber = "201",
            RoomType = "Deluxe",
            PricePerNight = pricePerNight,
            Status = RoomStatus.Occupied,
        };
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            TenantId = TestContext.TenantId,
            RoomId = room.Id,
            BookingNumber = "BK-0001",
            GuestName = "Guest",
            GuestPhone = "+910000000001",
            CheckInDate = DateTime.SpecifyKind(new DateTime(2026, 6, 10), DateTimeKind.Utc),
            CheckOutDate = DateTime.SpecifyKind(new DateTime(2026, 6, 10).AddDays(nights), DateTimeKind.Utc),
            TotalNights = nights,
            TotalAmount = pricePerNight * nights,
            AdvancePaid = advance > 0,
            AdvanceAmount = advance,
            BalanceAmount = pricePerNight * nights - advance,
            Status = BookingStatus.CheckedIn,
        };
        db.Rooms.Add(room);
        db.Bookings.Add(booking);
        db.SaveChanges();
        return (db, booking);
    }

    private static GenerateBillCommandHandler NewHandler(Infrastructure.Data.ApplicationDbContext db)
        => new(db, new TestContext.FakeTenantService(), new TestContext.NullNotificationRecorder());

    [Fact]
    public async Task Computes_subtotal_discount_tax_and_balance()
    {
        var (db, booking) = Seed();
        using var _ = db;

        var billId = await NewHandler(db).Handle(new GenerateBillCommand(
            booking.Id,
            ExtraServices: new() { new BillServiceItem("Laundry", 200m, 2) },
            DiscountAmount: 100m,
            TaxPercent: 10m,
            Notes: null), default);

        var bill = db.Bills.Single(b => b.Id == billId);

        Assert.Equal(3400m, bill.SubTotal);      // 1000*3 room + 200*2 laundry
        Assert.Equal(100m, bill.DiscountAmount);
        Assert.Equal(330m, bill.TaxAmount);      // (3400 - 100) * 10%
        Assert.Equal(3630m, bill.TotalAmount);   // 3400 - 100 + 330
        Assert.Equal(500m, bill.AmountPaid);     // carried from the booking advance
        Assert.Equal(3130m, bill.BalanceDue);
    }

    [Fact]
    public async Task Balance_due_never_goes_negative_when_advance_exceeds_total()
    {
        var (db, booking) = Seed(pricePerNight: 100m, nights: 1, advance: 5000m);
        using var _ = db;

        var billId = await NewHandler(db).Handle(new GenerateBillCommand(
            booking.Id, new(), 0m, 0m, null), default);

        var bill = db.Bills.Single(b => b.Id == billId);

        Assert.Equal(100m, bill.TotalAmount);
        Assert.Equal(0m, bill.BalanceDue);
    }

    [Fact]
    public async Task Generating_a_bill_twice_returns_the_same_bill()
    {
        var (db, booking) = Seed();
        using var _ = db;

        var first = await NewHandler(db).Handle(new GenerateBillCommand(booking.Id, new(), 0m, 0m, null), default);
        var second = await NewHandler(db).Handle(new GenerateBillCommand(booking.Id, new(), 0m, 0m, null), default);

        Assert.Equal(first, second);
        Assert.Single(db.Bills);
    }
}
