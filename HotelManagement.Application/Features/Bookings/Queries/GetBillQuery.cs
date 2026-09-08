using MediatR;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;

namespace HotelManagement.Application.Features.Bookings.Queries;

public record GetBillQuery(Guid BookingId) : IRequest<BillDto?>;

public class BillDto
{
    public Guid Id { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public Guid BookingId { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }

    // guest info
    public string GuestName { get; set; } = string.Empty;
    public string GuestPhone { get; set; } = string.Empty;
    public string? GuestAddress { get; set; }

    // room info
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int TotalNights { get; set; }

    // bill amounts
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public string? Notes { get; set; }

    public List<BillItemDto> Items { get; set; } = new();
}

public class BillItemDto
{
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
}

public class GetBillQueryHandler : IRequestHandler<GetBillQuery, BillDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;

    public GetBillQueryHandler(IApplicationDbContext context, ITenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    public async Task<BillDto?> Handle(GetBillQuery request, CancellationToken ct)
    {
        var bill = await _context.Bills
            .Include(b => b.Items)
            .Include(b => b.Booking).ThenInclude(b => b.Room)
            .FirstOrDefaultAsync(b => b.BookingId == request.BookingId
                && b.TenantId == _tenantService.TenantId, ct);

        if (bill == null) return null;

        return new BillDto
        {
            Id = bill.Id,
            BillNumber = bill.BillNumber,
            BookingId = bill.BookingId,
            BookingNumber = bill.Booking.BookingNumber,
            GeneratedAt = bill.GeneratedAt,

            GuestName = bill.Booking.GuestName,
            GuestPhone = bill.Booking.GuestPhone,
            GuestAddress = bill.Booking.GuestAddress,

            RoomNumber = bill.Booking.Room.RoomNumber,
            RoomType = bill.Booking.Room.RoomType,
            CheckInDate = bill.Booking.CheckInDate,
            CheckOutDate = bill.Booking.CheckOutDate,
            TotalNights = bill.Booking.TotalNights,

            SubTotal = bill.SubTotal,
            DiscountAmount = bill.DiscountAmount,
            TaxAmount = bill.TaxAmount,
            TotalAmount = bill.TotalAmount,
            AmountPaid = bill.AmountPaid,
            BalanceDue = bill.BalanceDue,
            Notes = bill.Notes,

            Items = bill.Items.Select(i => new BillItemDto
            {
                Description = i.Description,
                Category = i.Category,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                Amount = i.Amount
            }).ToList()
        };
    }
}