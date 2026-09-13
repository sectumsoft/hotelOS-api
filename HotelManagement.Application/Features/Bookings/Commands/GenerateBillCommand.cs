using MediatR;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Domain.Entities;

namespace HotelManagement.Application.Features.Bookings.Commands;

// Tax is no longer supplied per bill — it's configured once in Settings
// (HotelSettings.TaxPercent) and applied here, backed out of the tax-inclusive
// room/service rates rather than added on top.
public record GenerateBillCommand(Guid BookingId, List<BillServiceItem> ExtraServices, decimal DiscountAmount, string? Notes) : IRequest<Guid>;

public record BillServiceItem(string Description, decimal Amount, int Quantity = 1);

public class GenerateBillCommandHandler : IRequestHandler<GenerateBillCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly INotificationRecorder _notify;

    public GenerateBillCommandHandler(IApplicationDbContext context, ITenantService tenantService, INotificationRecorder notify)
    {
        _context = context;
        _tenantService = tenantService;
        _notify = notify;
    }

    public async Task<Guid> Handle(GenerateBillCommand request, CancellationToken ct)
    {
        var booking = await _context.Bookings
            .Include(b => b.Room)
            .FirstOrDefaultAsync(b => b.Id == request.BookingId && b.TenantId == _tenantService.TenantId, ct);

        if (booking == null) throw new Exception("Booking not found");

        // check if bill already exists
        var existing = await _context.Bills.FirstOrDefaultAsync(b => b.BookingId == request.BookingId, ct);
        if (existing != null) return existing.Id;

        var hotelSettings = await _context.HotelSettings
            .FirstOrDefaultAsync(s => s.TenantId == _tenantService.TenantId, ct);
        var taxPercent = hotelSettings?.TaxPercent ?? 0m;

        // ── validate money inputs — this all comes straight from the client ──
        if (request.DiscountAmount < 0)
            throw new Exception("Discount cannot be negative");
        foreach (var svc in request.ExtraServices)
        {
            if (svc.Amount < 0) throw new Exception($"'{svc.Description}' has a negative amount");
            if (svc.Quantity < 1) throw new Exception($"'{svc.Description}' has an invalid quantity");
        }

        var bill = new Bill
        {
            TenantId = _tenantService.TenantId,
            BookingId = booking.Id,
            BillNumber = $"BILL-{DateTime.UtcNow:yyyyMMdd}-{booking.BookingNumber}",
            GeneratedAt = DateTime.UtcNow,
            Notes = request.Notes
        };

        // 1. room charges line item — billed at the rate the guest actually agreed
        // to at booking time (booking.TotalAmount), not the room's current price.
        // The room's nightly rate can change after the booking is made (seasonal
        // pricing, a rate correction, …); re-deriving the charge from Room.PricePerNight
        // would silently re-price every past booking's bill to today's rate.
        var roomCharge = booking.TotalAmount;
        var nightlyRate = booking.TotalNights > 0 ? roomCharge / booking.TotalNights : roomCharge;
        bill.Items.Add(new BillItem
        {
            Description = $"Room {booking.Room.RoomNumber} ({booking.TotalNights} nights x ₹{nightlyRate:0.##})",
            Category = "Room",
            UnitPrice = nightlyRate,
            Quantity = booking.TotalNights,
            Amount = roomCharge
        });

        // 2. extra services
        foreach (var svc in request.ExtraServices)
        {
            bill.Items.Add(new BillItem
            {
                Description = svc.Description,
                Category = "Service",
                UnitPrice = svc.Amount,
                Quantity = svc.Quantity,
                Amount = svc.Amount * svc.Quantity
            });
        }

        // 3. calculate subtotal
        bill.SubTotal = bill.Items.Sum(i => i.Amount);

        if (request.DiscountAmount > bill.SubTotal)
            throw new Exception("Discount cannot exceed the subtotal");

        // 4. discount
        if (request.DiscountAmount > 0)
        {
            bill.Items.Add(new BillItem
            {
                Description = "Discount",
                Category = "Discount",
                UnitPrice = -request.DiscountAmount,
                Quantity = 1,
                Amount = -request.DiscountAmount
            });
            bill.DiscountAmount = request.DiscountAmount;
        }

        // 5. tax — room rates and service prices are tax-INCLUSIVE (configured once
        // in Settings), so tax is backed out of what's already being charged for
        // display, never added on top. The guest never owes more than the
        // subtotal they were already quoted; "Tax" here is a breakdown line, not
        // an extra charge, so it's deliberately not added to bill.Items (which
        // sums to the amount actually charged).
        var payableAmount = bill.SubTotal - bill.DiscountAmount;
        bill.TaxAmount = taxPercent > 0
            ? Math.Round(payableAmount - (payableAmount / (1 + taxPercent / 100)), 2)
            : 0m;

        // 6. totals — TotalAmount is the inclusive price itself, not
        // payableAmount + TaxAmount (that would double-count the tax already
        // baked into payableAmount).
        bill.TotalAmount = payableAmount;

        // AmountPaid must reflect everything collected so far, not just the
        // booking-time advance: CheckInCommand can take an additional payment at
        // check-in, but it records that by decrementing booking.BalanceAmount —
        // it does NOT touch booking.AdvanceAmount. Using AdvanceAmount here would
        // silently ignore that top-up and show a balance still due on a booking
        // that was already paid in full at check-in.
        bill.AmountPaid = booking.TotalAmount - booking.BalanceAmount;
        bill.BalanceDue = Math.Max(0, bill.TotalAmount - bill.AmountPaid);

        _context.Bills.Add(bill);
        await _context.SaveChangesAsync(ct);

        await _notify.RecordAsync("bill-generated", "Bill generated",
            $"{bill.BillNumber} · {booking.GuestName} · ₹{bill.TotalAmount:0}", "/bookings", booking.Id.ToString(), ct);
        return bill.Id;
    }
}