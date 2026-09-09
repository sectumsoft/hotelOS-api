using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.API.Controllers;

public class SearchHit
{
    public string Type { get; set; } = "";   // room | booking | guest
    public string Label { get; set; } = "";
    public string Sub { get; set; } = "";
    public string Link { get; set; } = "";
}

public class SearchResultDto
{
    public List<SearchHit> Rooms { get; set; } = new();
    public List<SearchHit> Bookings { get; set; } = new();
    public List<SearchHit> Guests { get; set; } = new();
}

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantService _tenant;

    public SearchController(IApplicationDbContext db, ITenantService tenant)
    { _db = db; _tenant = tenant; }

    // GET /api/search?q=...  — quick jump across rooms, bookings and guests.
    [HttpGet]
    public async Task<ActionResult<ApiResponse<SearchResultDto>>> Search([FromQuery] string q)
    {
        var tid = _tenant.TenantId;
        var result = new SearchResultDto();

        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Ok(ApiResponse<SearchResultDto>.Ok(result));

        var like = $"%{q.Trim()}%";

        result.Rooms = await _db.Rooms.AsNoTracking()
            .Where(r => r.TenantId == tid && !r.IsDeleted &&
                        (EF.Functions.ILike(r.RoomNumber, like) || EF.Functions.ILike(r.RoomType, like)))
            .OrderBy(r => r.RoomNumber)
            .Take(5)
            .Select(r => new SearchHit
            {
                Type = "room",
                Label = "Room " + r.RoomNumber,
                Sub = r.RoomType + " · " + r.Status,
                Link = "/rooms"
            })
            .ToListAsync();

        result.Bookings = await _db.Bookings.AsNoTracking()
            .Include(b => b.Room)
            .Where(b => b.TenantId == tid && !b.IsDeleted &&
                        (EF.Functions.ILike(b.BookingNumber, like) ||
                         EF.Functions.ILike(b.GuestName, like) ||
                         EF.Functions.ILike(b.GuestPhone, like)))
            .OrderByDescending(b => b.CreatedAt)
            .Take(5)
            .Select(b => new SearchHit
            {
                Type = "booking",
                Label = b.BookingNumber + " · " + b.GuestName,
                Sub = "Room " + (b.Room != null ? b.Room.RoomNumber : "—") + " · " + b.Status,
                Link = "/bookings"
            })
            .ToListAsync();

        result.Guests = await _db.Guests.AsNoTracking()
            .Where(g => g.TenantId == tid && !g.IsDeleted &&
                        _db.Bookings.Any(b => b.GuestId == g.Id) &&
                        (EF.Functions.ILike(g.Name, like) ||
                         EF.Functions.ILike(g.Phone, like) ||
                         (g.Email != null && EF.Functions.ILike(g.Email, like))))
            .OrderBy(g => g.Name)
            .Take(5)
            .Select(g => new SearchHit
            {
                Type = "guest",
                Label = g.Name,
                Sub = g.Phone,
                Link = "/guests"
            })
            .ToListAsync();

        return Ok(ApiResponse<SearchResultDto>.Ok(result));
    }
}
