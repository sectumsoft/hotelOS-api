using MediatR;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Domain.Enums;

namespace HotelManagement.Application.Features.Dashboard.Queries;

public record GetDashboardStatsQuery : IRequest<DashboardStatsDto>;
public record GetRevenueQuery(int Days = 30) : IRequest<List<RevenueDataPoint>>;
public record GetOccupancyQuery(int Days = 30) : IRequest<List<OccupancyDataPoint>>;
public record GetBookingSourcesQuery : IRequest<List<BookingSourceDto>>;

public class DashboardStatsDto
{
    public int TotalRooms { get; set; }
    public int OccupiedRooms { get; set; }
    public int AvailableRooms { get; set; }
    public int TodayBookings { get; set; }
    public decimal RevenueToday { get; set; }
    public double OccupancyRate { get; set; }
    public double RevenueChange { get; set; }
    public double BookingChange { get; set; }
}

public record RevenueDataPoint(string Date, decimal Amount);
public record OccupancyDataPoint(string Date, double Rate);
public class BookingSourceDto { public string Source { get; set; } = ""; public int Count { get; set; } public double Percentage { get; set; } }

public class GetDashboardStatsHandler : IRequestHandler<GetDashboardStatsQuery, DashboardStatsDto>
{
    private readonly IApplicationDbContext _ctx;
    private readonly ITenantService _ts;

    public GetDashboardStatsHandler(IApplicationDbContext ctx, ITenantService ts)
    { _ctx = ctx; _ts = ts; }

    public async Task<DashboardStatsDto> Handle(GetDashboardStatsQuery req, CancellationToken ct)
    {
        var tenantId = _ts.TenantId;
        var today = DateTime.UtcNow.Date;

        var totalRooms = await _ctx.Rooms.CountAsync(r => r.TenantId == tenantId && !r.IsDeleted, ct);
        var occupiedRooms = await _ctx.Rooms.CountAsync(r => r.TenantId == tenantId && r.Status == RoomStatus.Occupied && !r.IsDeleted, ct);
        var todayBookings = await _ctx.Bookings.CountAsync(b => b.TenantId == tenantId && b.CreatedAt.Date == today, ct);
        var revenueToday = await _ctx.Payments.Where(p => p.TenantId == tenantId && p.CreatedAt.Date == today).SumAsync(p => p.Amount, ct);

        return new DashboardStatsDto
        {
            TotalRooms = totalRooms,
            OccupiedRooms = occupiedRooms,
            AvailableRooms = totalRooms - occupiedRooms,
            TodayBookings = todayBookings,
            RevenueToday = revenueToday,
            OccupancyRate = totalRooms > 0 ? Math.Round((double)occupiedRooms / totalRooms * 100, 1) : 0,
            RevenueChange = 12.5,
            BookingChange = 8.3
        };
    }
}
