using HotelManagement.Application.Common.Interfaces;
using HotelManagement.API.Middleware;
using HotelManagement.Application.Common.Models;
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
    public string CreatedAt { get; set; } = "";
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

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<GuestDto>>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _context.Guests.Where(g => g.TenantId == _tenantService.TenantId && !g.IsDeleted);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(g => g.Name.Contains(search) || g.Phone.Contains(search));

        var total = await query.CountAsync();
        var items = await query.OrderBy(g => g.Name)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

        var dtos = items.Select(g => new GuestDto
        {
            Id = g.Id, Name = g.Name, Email = g.Email, Phone = g.Phone,
            Address = g.Address, TotalStays = g.TotalStays,
            CreatedAt = g.CreatedAt.ToString("yyyy-MM-dd")
        }).ToList();

        return Ok(ApiResponse<PagedResult<GuestDto>>.Ok(new PagedResult<GuestDto>
        { Items = dtos, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize }));
    }
}
