using MediatR;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Domain.Entities;

namespace HotelManagement.Application.Features.Bookings.Commands;

public record GenerateBillCommand(Guid BookingId, List<BillServiceItem> ExtraServices, decimal DiscountAmount, decimal TaxPercent, string? Notes) : IRequest<Guid>;

public record BillServiceItem(string Description, decimal Amount, int Quantity = 1);

public class GenerateBillCommandHandler : IRequestHandler<GenerateBillCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;

    public GenerateBillCommandHandler(IApplicationDbContext context, ITenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
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

        var bill = new Bill
        {
            TenantId = _tenantService.TenantId,
            BookingId = booking.Id,
            BillNumber = $"BILL-{DateTime.UtcNow:yyyyMMdd}-{booking.BookingNumber}",
            GeneratedAt = DateTime.UtcNow,
            Notes = request.Notes
        };

        // 1. room charges line item
        var roomCharge = booking.Room.PricePerNight * booking.TotalNights;
        bill.Items.Add(new BillItem
        {
            Description = $"Room {booking.Room.RoomNumber} ({booking.TotalNights} nights x ${booking.Room.PricePerNight})",
            Category = "Room",
            UnitPrice = booking.Room.PricePerNight,
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

        // 5. tax
        var taxableAmount = bill.SubTotal - bill.DiscountAmount;
        bill.TaxAmount = Math.Round(taxableAmount * (request.TaxPercent / 100), 2);
        if (bill.TaxAmount > 0)
        {
            bill.Items.Add(new BillItem
            {
                Description = $"Tax ({request.TaxPercent}%)",
                Category = "Tax",
                UnitPrice = bill.TaxAmount,
                Quantity = 1,
                Amount = bill.TaxAmount
            });
        }

        // 6. totals
        bill.TotalAmount = bill.SubTotal - bill.DiscountAmount + bill.TaxAmount;
        bill.AmountPaid = booking.AdvanceAmount;
        bill.BalanceDue = Math.Max(0, bill.TotalAmount - bill.AmountPaid);

        _context.Bills.Add(bill);
        await _context.SaveChangesAsync(ct);
        return bill.Id;
    }
}