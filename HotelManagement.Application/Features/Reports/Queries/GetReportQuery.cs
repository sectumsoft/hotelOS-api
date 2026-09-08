using MediatR;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Application.Common.Models;
using HotelManagement.Domain.Enums;

namespace HotelManagement.Application.Features.Reports.Queries;

public record GetReportQuery(DateTime DateFrom, DateTime DateTo, string? RoomType, string? Status, int PageNumber = 1, int PageSize = 50) : IRequest<PagedResult<ReportRowDto>>;

public class ReportRowDto
{
    public string BookingNumber { get; set; } = "";
    public string GuestName { get; set; } = "";
    public string RoomNumber { get; set; } = "";
    public string RoomType { get; set; } = "";
    public string CheckInDate { get; set; } = "";
    public string CheckOutDate { get; set; } = "";
    public int Nights { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AdvanceAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public string Status { get; set; } = "";
}

public class GetReportQueryHandler : IRequestHandler<GetReportQuery, PagedResult<ReportRowDto>>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantService _ts;

    public GetReportQueryHandler(IApplicationDbContext ctx, ITenantService ts) { _ctx = ctx; _ts = ts; }

    public async Task<PagedResult<ReportRowDto>> Handle(GetReportQuery req, CancellationToken ct)
    {
        var query = _ctx.Bookings.Include(b => b.Room)
            .Where(b => b.TenantId == _ts.TenantId && !b.IsDeleted
                     && b.CheckInDate >= req.DateFrom && b.CheckInDate <= req.DateTo);

        if (!string.IsNullOrEmpty(req.Status) && Enum.TryParse<BookingStatus>(req.Status, out var bs))
            query = query.Where(b => b.Status == bs);
        if (!string.IsNullOrEmpty(req.RoomType))
            query = query.Where(b => b.Room.RoomType == req.RoomType);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(b => b.CheckInDate)
            .Skip((req.PageNumber - 1) * req.PageSize).Take(req.PageSize).ToListAsync(ct);

        var rows = items.Select(b => new ReportRowDto
        {
            BookingNumber = b.BookingNumber, GuestName = b.GuestName,
            RoomNumber = b.Room.RoomNumber, RoomType = b.Room.RoomType,
            CheckInDate = b.CheckInDate.ToString("yyyy-MM-dd"), CheckOutDate = b.CheckOutDate.ToString("yyyy-MM-dd"),
            Nights = b.TotalNights, TotalAmount = b.TotalAmount, AdvanceAmount = b.AdvanceAmount,
            BalanceAmount = b.BalanceAmount, Status = b.Status.ToString()
        }).ToList();

        return new PagedResult<ReportRowDto> { Items = rows, TotalCount = total, PageNumber = req.PageNumber, PageSize = req.PageSize };
    }
}
