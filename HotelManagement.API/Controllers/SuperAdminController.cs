using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Application.Common.Models;
using HotelManagement.Domain.Entities;
using HotelManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Numerics;

namespace HotelManagement.API.Controllers;

// ── DTOs ──────────────────────────────────────────────
public class OnboardHotelDto
{
    public string HotelName { get; set; } = "";
    public string Subdomain { get; set; } = "";
    public string AdminName { get; set; } = "";
    public string AdminEmail { get; set; } = "";
    public string TempPassword { get; set; } = "";
    public int Plan { get; set; } = 1;
}

public class AddAdminDto
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string TempPassword { get; set; } = "";
}

public class HotelListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Subdomain { get; set; } = "";
    public bool IsActive { get; set; }
    public int UserCount { get; set; }
    public string CreatedAt { get; set; } = "";
}

public class UserListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public bool IsActive { get; set; }
    public List<string> Modules { get; set; } = new();
}

// ── Controller ────────────────────────────────────────
[Authorize(Roles = "SuperAdmin")]
[ApiController]
[Route("api/superadmin")]
public class SuperAdminController : ControllerBase
{
    private readonly IApplicationDbContext _db;

    public SuperAdminController(IApplicationDbContext db) { _db = db; }

    // GET /api/superadmin/hotels
    [HttpGet("hotels")]
    public async Task<ActionResult<ApiResponse<List<HotelListDto>>>> GetHotels()
    {
        var hotels = await _db.Tenants
            .Select(t => new HotelListDto
            {
                Id = t.Id,
                Name = t.Name,
                Subdomain = t.Subdomain,
                IsActive = t.IsActive,
                UserCount = _db.Users.Count(u => u.TenantId == t.Id && !u.IsDeleted),
                CreatedAt = t.CreatedAt.ToString("yyyy-MM-dd")
            }).ToListAsync();

        return Ok(ApiResponse<List<HotelListDto>>.Ok(hotels));
    }

    // POST /api/superadmin/onboard  — creates hotel + first admin
    [HttpPost("onboard")]
    public async Task<ActionResult<ApiResponse<Guid>>> OnboardHotel(OnboardHotelDto dto)
    {
        // 1. Check subdomain is unique
        var exists = await _db.Tenants.AnyAsync(t => t.Subdomain == dto.Subdomain);
        if (exists)
            return BadRequest(ApiResponse<Guid>.Fail("Subdomain already taken"));

        // 2. Create hotel (Tenant)
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = dto.HotelName,
            Subdomain = dto.Subdomain,
            Plan = (TenantPlan)dto.Plan,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Tenants.Add(tenant);

        // 3. Create first HotelAdmin for this hotel
        var admin = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = dto.AdminName,
            Email = dto.AdminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.TempPassword),
            Role = UserRole.HotelAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(admin);

        // 4. Give the new hotel the default room types (editable in Settings)
        foreach (var name in new[] { "Standard", "Deluxe", "Suite" })
            _db.RoomTypes.Add(new HotelManagement.Domain.Entities.RoomType { TenantId = tenant.Id, Name = name });

        await _db.SaveChangesAsync(CancellationToken.None);

        return Ok(ApiResponse<Guid>.Ok(tenant.Id, "Hotel onboarded successfully"));
    }

    // GET /api/superadmin/hotels/{tenantId}/users
    [HttpGet("hotels/{tenantId}/users")]
    public async Task<ActionResult<ApiResponse<List<UserListDto>>>> GetHotelUsers(Guid tenantId)
    {
        var users = await _db.Users
            .Where(u => u.TenantId == tenantId && !u.IsDeleted)
            .Select(u => new UserListDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role.ToString(),
                IsActive = u.IsActive
            }).ToListAsync();

        return Ok(ApiResponse<List<UserListDto>>.Ok(users));
    }

    // POST /api/superadmin/hotels/{tenantId}/add-admin
    [HttpPost("hotels/{tenantId}/add-admin")]
    public async Task<ActionResult<ApiResponse<Guid>>> AddAdmin(Guid tenantId, AddAdminDto dto)
    {
        var hotel = await _db.Tenants.FindAsync(tenantId);
        if (hotel == null)
            return NotFound(ApiResponse<Guid>.Fail("Hotel not found"));

        var admin = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = dto.Name,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.TempPassword),
            Role = UserRole.HotelAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(admin);
        await _db.SaveChangesAsync(CancellationToken.None);

        return Ok(ApiResponse<Guid>.Ok(admin.Id, "Admin added successfully"));
    }
}