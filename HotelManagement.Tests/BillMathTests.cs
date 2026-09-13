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

    [Fact]
    public async Task Bills_at_the_rate_locked_in_at_booking_time_not_the_rooms_current_price()
    {
        var (db, booking) = Seed(pricePerNight: 1000m, nights: 3); // booking.TotalAmount = 3000
        using var _ = db;

        // The room's rate changes after the booking was made (e.g. a seasonal
        // price update) — the bill must still reflect what the guest agreed to.
        db.Rooms.Single(r => r.Id == booking.RoomId).PricePerNight = 5000m;
        db.SaveChanges();

        var billId = await NewHandler(db).Handle(new GenerateBillCommand(booking.Id, new(), 0m, 0m, null), default);
        var bill = db.Bills.Single(b => b.Id == billId);

        Assert.Equal(3000m, bill.SubTotal);
        Assert.Equal(3000m, bill.TotalAmount);
    }

    [Fact]
    public async Task Rejects_a_discount_larger_than_the_subtotal()
    {
        var (db, booking) = Seed(pricePerNight: 1000m, nights: 1); // subtotal = 1000
        using var _ = db;

        var ex = await Assert.ThrowsAsync<Exception>(() => NewHandler(db).Handle(
            new GenerateBillCommand(booking.Id, new(), DiscountAmount: 5000m, TaxPercent: 0m, Notes: null), default));

        Assert.Contains("exceed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(150)]
    public async Task Rejects_a_tax_percent_outside_0_to_100(decimal badTaxPercent)
    {
        var (db, booking) = Seed();
        using var _ = db;

        await Assert.ThrowsAsync<Exception>(() => NewHandler(db).Handle(
            new GenerateBillCommand(booking.Id, new(), 0m, badTaxPercent, null), default));
    }

    [Fact]
    public async Task Rejects_a_negative_extra_service_amount()
    {
        var (db, booking) = Seed();
        using var _ = db;

        await Assert.ThrowsAsync<Exception>(() => NewHandler(db).Handle(
            new GenerateBillCommand(booking.Id, new() { new BillServiceItem("Refund abuse", -500m) }, 0m, 0m, null), default));
    }
}
