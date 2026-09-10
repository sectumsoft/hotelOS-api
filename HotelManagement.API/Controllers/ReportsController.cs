using System.Text;
using HotelManagement.API.Middleware;
using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Application.Common.Models;
using HotelManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.API.Controllers;

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

[Authorize]
[ApiController]
[Route("api/[controller]")]
[ModuleAccess("reports")]
public class ReportsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;

    public ReportsController(IApplicationDbContext ctx, ITenantService ts)
    { _context = ctx; _tenantService = ts; }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ReportRowDto>>>> GetReport(
        [FromQuery] DateTime dateFrom,
        [FromQuery] DateTime dateTo,
        [FromQuery] string? roomType,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _context.Bookings.Include(b => b.Room)
            .Where(b => b.TenantId == _tenantService.TenantId && !b.IsDeleted
                && b.CheckInDate >= dateFrom && b.CheckInDate <= dateTo);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<BookingStatus>(status, out var bs))
            query = query.Where(b => b.Status == bs);
        if (!string.IsNullOrEmpty(roomType))
            query = query.Where(b => b.Room.RoomType == roomType);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(b => b.CheckInDate)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

        var rows = items.Select(b => new ReportRowDto
        {
            BookingNumber = b.BookingNumber,
            GuestName = b.GuestName,
            RoomNumber = b.Room.RoomNumber,
            RoomType = b.Room.RoomType,
            CheckInDate = b.CheckInDate.ToString("yyyy-MM-dd"),
            CheckOutDate = b.CheckOutDate.ToString("yyyy-MM-dd"),
            Nights = b.TotalNights,
            TotalAmount = b.TotalAmount,
            AdvanceAmount = b.AdvanceAmount,
            BalanceAmount = b.BalanceAmount,
            Status = b.Status.ToString()
        }).ToList();

        return Ok(ApiResponse<PagedResult<ReportRowDto>>.Ok(new PagedResult<ReportRowDto>
        { Items = rows, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize }));
    }

    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportCsv([FromQuery] DateTime dateFrom, [FromQuery] DateTime dateTo)
    {
        var bookings = await _context.Bookings.Include(b => b.Room)
            .Where(b => b.TenantId == _tenantService.TenantId && !b.IsDeleted
                && b.CheckInDate >= dateFrom && b.CheckInDate <= dateTo)
            .OrderByDescending(b => b.CheckInDate).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Booking ID,Guest Name,Room,Type,Check-In,Check-Out,Nights,Total,Advance,Balance,Status");
        foreach (var b in bookings)
            sb.AppendLine($"{b.BookingNumber},{b.GuestName},{b.Room.RoomNumber},{b.Room.RoomType},{b.CheckInDate:yyyy-MM-dd},{b.CheckOutDate:yyyy-MM-dd},{b.TotalNights},{b.TotalAmount},{b.AdvanceAmount},{b.BalanceAmount},{b.Status}");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"hotel-report-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // Excel export is generated client-side as a real .xlsx from the loaded rows
    // (see ReportsComponent.exportExcel on the frontend). The old server action
    // returned CSV with an .xlsx name, which Excel flagged as corrupt.
}
