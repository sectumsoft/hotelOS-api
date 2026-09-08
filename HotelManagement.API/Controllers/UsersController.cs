using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Application.Common.Models;
using HotelManagement.Domain.Entities;
using HotelManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.API.Controllers;

public static class StaffModules
{
    public static readonly string[] All = { "rooms", "bookings", "guests", "reports" };

    public static string Normalize(IEnumerable<string>? modules) =>
        string.Join(",", (modules ?? Enumerable.Empty<string>())
            .Select(m => m?.Trim().ToLowerInvariant() ?? "")
            .Where(m => All.Contains(m))
            .Distinct());

    public static List<string> Split(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? new List<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

public class CreateStaffDto
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string TempPassword { get; set; } = "";
    public List<string> Modules { get; set; } = new();
}

public class UpdateStaffDto
{
    public string Name { get; set; } = "";
    public List<string> Modules { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public string? NewPassword { get; set; }
}

[Authorize(Roles = "HotelAdmin")]
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantService _tenantService;

    public UsersController(IApplicationDbContext db, ITenantService ts)
    { _db = db; _tenantService = ts; }

    [HttpGet("modules")]
    public ActionResult<ApiResponse<string[]>> GetModules()
        => Ok(ApiResponse<string[]>.Ok(StaffModules.All));

    // GET /api/users  — staff of THIS hotel only
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<UserListDto>>>> GetStaff()
    {
        var tenantId = _tenantService.TenantId;

        var users = await _db.Users
            .Where(u => u.TenantId == tenantId && !u.IsDeleted && u.Role == UserRole.Staff)
            .OrderBy(u => u.Name)
            .Select(u => new UserListDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role.ToString(),
                IsActive = u.IsActive,
                Modules = StaffModules.Split(u.Permissions)
            }).ToListAsync();

        return Ok(ApiResponse<List<UserListDto>>.Ok(users));
    }

    // POST /api/users  — create staff for THIS hotel only
    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> CreateStaff(CreateStaffDto dto)
    {
        var tenantId = _tenantService.TenantId;

        var name = dto.Name?.Trim() ?? "";
        var email = dto.Email?.Trim().ToLowerInvariant() ?? "";

        if (string.IsNullOrWhiteSpace(name)) return BadRequest(ApiResponse<Guid>.Fail("Name is required"));
        if (string.IsNullOrWhiteSpace(email)) return BadRequest(ApiResponse<Guid>.Fail("Email is required"));
        if (string.IsNullOrWhiteSpace(dto.TempPassword) || dto.TempPassword.Length < 6)
            return BadRequest(ApiResponse<Guid>.Fail("Temporary password must be at least 6 characters"));

        var taken = await _db.Users.AnyAsync(u => u.TenantId == tenantId && u.Email == email && !u.IsDeleted);
        if (taken) return BadRequest(ApiResponse<Guid>.Fail("A user with that email already exists"));

        var staff = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.TempPassword),
            Role = UserRole.Staff,
            IsActive = true,
            Permissions = StaffModules.Normalize(dto.Modules),
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(staff);
        await _db.SaveChangesAsync(CancellationToken.None);

        return Ok(ApiResponse<Guid>.Ok(staff.Id, "Staff created successfully"));
    }

    // PUT /api/users/{id}  — update name / module access / active / (optional) password
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateStaff(Guid id, UpdateStaffDto dto)
    {
        var tenantId = _tenantService.TenantId;

        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Id == id && u.TenantId == tenantId && u.Role == UserRole.Staff && !u.IsDeleted);
        if (user == null) return NotFound(ApiResponse<bool>.Fail("Staff member not found"));

        if (!string.IsNullOrWhiteSpace(dto.Name)) user.Name = dto.Name.Trim();
        user.Permissions = StaffModules.Normalize(dto.Modules);
        user.IsActive = dto.IsActive;

        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            if (dto.NewPassword.Length < 6)
                return BadRequest(ApiResponse<bool>.Fail("New password must be at least 6 characters"));
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(CancellationToken.None);

        return Ok(ApiResponse<bool>.Ok(true, "Staff updated"));
    }

    // DELETE /api/users/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteStaff(Guid id)
    {
        var tenantId = _tenantService.TenantId;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId && u.Role == UserRole.Staff);

        if (user == null)
            return NotFound(ApiResponse<bool>.Fail("User not found"));

        user.IsDeleted = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(CancellationToken.None);

        return Ok(ApiResponse<bool>.Ok(true, "Staff removed"));
    }
}
