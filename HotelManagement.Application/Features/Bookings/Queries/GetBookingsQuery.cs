using MediatR;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Application.Common.Models;

namespace HotelManagement.Application.Features.Bookings.Queries;

public record GetBookingsQuery(string? Search, string? Status, DateTime? CheckInFrom, DateTime? CheckInTo, int PageNumber = 1, int PageSize = 12) : IRequest<PagedResult<BookingDto>>;

public class BookingDto
{
    public Guid Id { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public Guid GuestId { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string GuestPhone { get; set; } = string.Empty;
    public string? GuestAddress { get; set; }
    public Guid RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int TotalNights { get; set; }
    public decimal TotalAmount { get; set; }
    public bool AdvancePaid { get; set; }
    public decimal AdvanceAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GetBookingsQueryHandler : IRequestHandler<GetBookingsQuery, PagedResult<BookingDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly IMapper _mapper;

    public GetBookingsQueryHandler(IApplicationDbContext ctx, ITenantService ts, IMapper m)
    { _context = ctx; _tenantService = ts; _mapper = m; }

    public async Task<PagedResult<BookingDto>> Handle(GetBookingsQuery req, CancellationToken ct)
    {
        var query = _context.Bookings
            .Include(b => b.Room)
            .Where(b => b.TenantId == _tenantService.TenantId && !b.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.Search))
            query = query.Where(b => b.GuestName.Contains(req.Search) || b.BookingNumber.Contains(req.Search) || b.Room.RoomNumber.Contains(req.Search));
        if (!string.IsNullOrWhiteSpace(req.Status) && Enum.TryParse<HotelManagement.Domain.Enums.BookingStatus>(req.Status, out var status))
            query = query.Where(b => b.Status == status);
        if (req.CheckInFrom.HasValue) query = query.Where(b => b.CheckInDate >= req.CheckInFrom.Value);
        if (req.CheckInTo.HasValue) query = query.Where(b => b.CheckInDate <= req.CheckInTo.Value);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(b => b.CreatedAt).Skip((req.PageNumber-1)*req.PageSize).Take(req.PageSize).ToListAsync(ct);

        return new PagedResult<BookingDto> { Items = _mapper.Map<List<BookingDto>>(items), TotalCount = total, PageNumber = req.PageNumber, PageSize = req.PageSize };
    }
}
