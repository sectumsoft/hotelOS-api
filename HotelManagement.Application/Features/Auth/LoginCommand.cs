using MediatR;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;

namespace HotelManagement.Application.Features.Auth;

public record LoginCommand(string Email, string Password) : IRequest<LoginResultDto?>;

public class LoginResultDto
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
    public TenantDto? Tenant { get; set; } = null!;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public string? Avatar { get; set; }
}

public class TenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public string? Logo { get; set; }
    public string Plan { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResultDto?>
{
    private readonly IApplicationDbContext _ctx;
    private readonly IJwtService _jwt;

    public LoginCommandHandler(IApplicationDbContext ctx, IJwtService jwt)
    { _ctx = ctx; _jwt = jwt; }

    public async Task<LoginResultDto?> Handle(LoginCommand req, CancellationToken ct)
    {
        var user = await _ctx.Users.Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == req.Email && u.IsActive, ct);

        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash)) return null;

        var token = _jwt.GenerateToken(user);
        var refresh = _jwt.GenerateRefreshToken();
        user.RefreshToken = refresh;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _ctx.SaveChangesAsync(ct);

        return new LoginResultDto
        {
            Token = token,
            RefreshToken = refresh,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            User = new UserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role.ToString(),
                TenantId = user.TenantId,
                Avatar = user.AvatarUrl
            },
            Tenant = user.Tenant == null ? null : new TenantDto  // ← null check here
            {
                Id = user.Tenant.Id,
                Name = user.Tenant.Name,
                Subdomain = user.Tenant.Subdomain,
                Logo = user.Tenant.LogoUrl,
                Plan = user.Tenant.Plan.ToString(),
                IsActive = user.Tenant.IsActive
            }
        };
    }
}
