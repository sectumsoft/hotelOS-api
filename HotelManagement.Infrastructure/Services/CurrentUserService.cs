using HotelManagement.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace HotelManagement.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;
    public CurrentUserService(IHttpContextAccessor http) { _http = http; }

    public Guid UserId => Guid.TryParse(_http.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;
    public Guid TenantId => Guid.TryParse(_http.HttpContext?.User.FindFirst("tenantId")?.Value, out var id) ? id : Guid.Empty;
    public string Role => _http.HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    public bool IsAuthenticated => _http.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
