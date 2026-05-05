using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Application.Common.Models;
using HotelManagement.Domain.Entities;
using HotelManagement.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HotelManagement.API.Controllers;

public class CreateStaffDto
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string TempPassword { get; set; } = "";
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

    // GET /api/users  — list staff of THIS hotel only
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<UserListDto>>>> GetStaff()
    {
        var tenantId = _tenantService.TenantId; // from JWT, can't be faked

        var users = await _db.Users
            .Where(u => u.TenantId == tenantId && !u.IsDeleted && u.Role == UserRole.Staff)
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

    // POST /api/users  — create staff for THIS hotel only
    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> CreateStaff(CreateStaffDto dto)
    {
        var tenantId = _tenantService.TenantId; // always from JWT

        var staff = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,          // locked to their hotel
            Name = dto.Name,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.TempPassword),
            Role = UserRole.Staff,    // can only create Staff
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(staff);
        await _db.SaveChangesAsync(CancellationToken.None);

        return Ok(ApiResponse<Guid>.Ok(staff.Id, "Staff created successfully"));
    }

    // DELETE /api/users/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteStaff(Guid id)
    {
        var tenantId = _tenantService.TenantId;

        // tenantId check prevents deleting users from other hotels
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId);

        if (user == null)
            return NotFound(ApiResponse<bool>.Fail("User not found"));

        user.IsDeleted = true;
        await _db.SaveChangesAsync(CancellationToken.None);

        return Ok(ApiResponse<bool>.Ok(true, "Staff removed"));
    }
}