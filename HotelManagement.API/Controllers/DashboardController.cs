using HotelManagement.Application.Common.Models;
using HotelManagement.Application.Features.Dashboard.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;
    public DashboardController(IMediator mediator) { _mediator = mediator; }

    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<DashboardStatsDto>>> GetStats()
    {
        var result = await _mediator.Send(new GetDashboardStatsQuery());
        return Ok(ApiResponse<DashboardStatsDto>.Ok(result));
    }

    [HttpGet("revenue")]
    public ActionResult<ApiResponse<List<RevenueDataPoint>>> GetRevenue([FromQuery] int days = 30)
    {
        var rnd = new Random(42);
        var data = Enumerable.Range(0, days).Select(i =>
        {
            var date = DateTime.UtcNow.AddDays(-(days - 1 - i)).ToString("MMM d");
            return new RevenueDataPoint(date, (decimal)(rnd.NextDouble() * 4000 + 1500));
        }).ToList();
        return Ok(ApiResponse<List<RevenueDataPoint>>.Ok(data));
    }

    [HttpGet("occupancy")]
    public ActionResult<ApiResponse<List<OccupancyDataPoint>>> GetOccupancy([FromQuery] int days = 30)
    {
        var rnd = new Random(99);
        var data = Enumerable.Range(0, days).Select(i =>
        {
            var date = DateTime.UtcNow.AddDays(-(days - 1 - i)).ToString("MMM d");
            return new OccupancyDataPoint(date, Math.Round(rnd.NextDouble() * 40 + 50, 1));
        }).ToList();
        return Ok(ApiResponse<List<OccupancyDataPoint>>.Ok(data));
    }

    [HttpGet("booking-sources")]
    public ActionResult<ApiResponse<List<BookingSourceDto>>> GetBookingSources()
    {
        var data = new List<BookingSourceDto>
        {
            new() { Source = "Direct", Count = 45, Percentage = 45 },
            new() { Source = "Online (OTA)", Count = 30, Percentage = 30 },
            new() { Source = "Travel Agent", Count = 25, Percentage = 25 }
        };
        return Ok(ApiResponse<List<BookingSourceDto>>.Ok(data));
    }
}
