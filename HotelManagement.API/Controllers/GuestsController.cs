using HotelManagement.Application.Common.Interfaces;
using HotelManagement.API.Middleware;
using HotelManagement.Application.Common.Models;
using HotelManagement.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.API.Controllers;

public class GuestDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string Phone { get; set; } = "";
    public string? Address { get; set; }
    public int TotalStays { get; set; }
    public bool HasIdProof { get; set; }
    public string CreatedAt { get; set; } = "";
}

// ── Guest 360 ────────────────────────────────────────────────────────────────
public class GuestProfileDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string Phone { get; set; } = "";
    public string? Address { get; set; }
    public int TotalStays { get; set; }
    public string CreatedAt { get; set; } = "";
    public string? IdProofType { get; set; }
    public string? IdProofNumber { get; set; }
    public string? IdProofUrl { get; set; }
}

public class GuestStatsDto
{
    public int TotalBookings { get; set; }
    public int TotalNights { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal Outstanding { get; set; }
    public string? LastStay { get; set; }
}

public class CompanionDto
{
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? IdProofType { get; set; }
    public string? IdProofNumber { get; set; }
    public string? IdProofUrl { get; set; }
}

public class GuestBookingDto
{
    public string BookingNumber { get; set; } = "";
    public string RoomNumber { get; set; } = "";
    public string RoomType { get; set; } = "";
    public string CheckInDate { get; set; } = "";
    public string CheckOutDate { get; set; } = "";
    public int Nights { get; set; }
    public string Status { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public List<CompanionDto> Companions { get; set; } = new();
}

public class GuestDetailDto
{
    public GuestProfileDto Guest { get; set; } = new();
    public GuestStatsDto Stats { get; set; } = new();
    public List<GuestBookingDto> Bookings { get; set; } = new();
}

[Authorize]
[ApiController]
[Route("api/[controller]")]
[ModuleAccess("guests")]
public class GuestsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;

    public GuestsController(IApplicationDbContext ctx, ITenantService ts)
    { _context = ctx; _tenantService = ts; }

    // GET /api/guests — account holders only (companions added at check-in are
    // nested inside the 360 view, not listed here).
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<GuestDto>>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var tid = _tenantService.TenantId;

        var query = _context.Guests.AsNoTracking()
            .Where(g => g.TenantId == tid && !g.IsDeleted
                     && _context.Bookings.Any(b => b.GuestId == g.Id && !b.IsDeleted));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            query = query.Where(g =>
                EF.Functions.ILike(g.Name, s) ||
                EF.Functions.ILike(g.Phone, s) ||
                (g.Email != null && EF.Functions.ILike(g.Email, s)));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(g => g.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new GuestDto
            {
                Id = g.Id,
                Name = g.Name,
                Email = g.Email,
                Phone = g.Phone,
                Address = g.Address,
                TotalStays = g.TotalStays,
                HasIdProof = g.IdProofUrl != null,
                CreatedAt = g.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(ApiResponse<PagedResult<GuestDto>>.Ok(new PagedResult<GuestDto>
        { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize }));
    }

    // GET /api/guests/{id} — the "Guest 360": profile, lifetime stats, every
    // booking, and the companions (with ID proofs) captured at each check-in.
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<GuestDetailDto>>> GetById(Guid id)
    {
        var tid = _tenantService.TenantId;

        var guest = await _context.Guests.AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id && g.TenantId == tid && !g.IsDeleted);
        if (guest == null)
            return NotFound(ApiResponse<GuestDetailDto>.Fail("Guest not found"));

        var bookings = await _context.Bookings.AsNoTracking()
            .Include(b => b.Room)
            .Where(b => b.GuestId == id && b.TenantId == tid && !b.IsDeleted)
            .OrderByDescending(b => b.CheckInDate)
            .ToListAsync();

        var bookingIds = bookings.Select(b => b.Id).ToList();

        // one query for every companion across all this guest's bookings
        var companions = bookingIds.Count == 0
            ? new List<Guest>()
            : await _context.Guests.AsNoTracking()
                .Where(g => g.TenantId == tid && !g.IsDeleted && g.Id != id
                         && g.BookingId != null && bookingIds.Contains(g.BookingId.Value))
                .ToListAsync();

        var companionsByBooking = companions
            .GroupBy(g => g.BookingId!.Value)
            .ToDictionary(grp => grp.Key, grp => grp.ToList());

        var activeBookings = bookings.Where(b => b.Status.ToString() != "Cancelled").ToList();

        var detail = new GuestDetailDto
        {
            Guest = new GuestProfileDto
            {
                Id = guest.Id,
                Name = guest.Name,
                Email = guest.Email,
                Phone = guest.Phone,
                Address = guest.Address,
                TotalStays = guest.TotalStays,
                CreatedAt = guest.CreatedAt.ToString("yyyy-MM-dd"),
                IdProofType = guest.IdProofType,
                IdProofNumber = guest.IdProofNumber,
                IdProofUrl = guest.IdProofUrl
            },
            Stats = new GuestStatsDto
            {
                TotalBookings = bookings.Count,
                TotalNights = activeBookings.Sum(b => b.TotalNights),
                TotalSpent = activeBookings.Sum(b => b.TotalAmount),
                Outstanding = activeBookings.Sum(b => b.BalanceAmount),
                LastStay = bookings
                    .Where(b => b.Status.ToString() == "CheckedOut")
                    .OrderByDescending(b => b.CheckOutDate)
                    .Select(b => b.CheckOutDate.ToString("yyyy-MM-dd"))
                    .FirstOrDefault()
            },
            Bookings = bookings.Select(b => new GuestBookingDto
            {
                BookingNumber = b.BookingNumber,
                RoomNumber = b.Room != null ? b.Room.RoomNumber : "",
                RoomType = b.Room != null ? b.Room.RoomType : "",
                CheckInDate = b.CheckInDate.ToString("yyyy-MM-dd"),
                CheckOutDate = b.CheckOutDate.ToString("yyyy-MM-dd"),
                Nights = b.TotalNights,
                Status = b.Status.ToString(),
                TotalAmount = b.TotalAmount,
                BalanceAmount = b.BalanceAmount,
                Companions = companionsByBooking.TryGetValue(b.Id, out var list)
                    ? list.Select(c => new CompanionDto
                    {
                        Name = c.Name,
                        Phone = c.Phone,
                        IdProofType = c.IdProofType,
                        IdProofNumber = c.IdProofNumber,
                        IdProofUrl = c.IdProofUrl
                    }).ToList()
                    : new List<CompanionDto>()
            }).ToList()
        };

        return Ok(ApiResponse<GuestDetailDto>.Ok(detail));
    }
}
