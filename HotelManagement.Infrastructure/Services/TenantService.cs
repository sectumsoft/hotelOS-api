using HotelManagement.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace HotelManagement.Infrastructure.Services;

public class TenantService : ITenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantService(IHttpContextAccessor httpContextAccessor)
    { _httpContextAccessor = httpContextAccessor; }

    public Guid TenantId
    {
        get
        {
            // The tenantId claim is baked into the JWT at login and can't be forged
            // without the signing key. We deliberately do NOT fall back to a client
            // header (e.g. X-Tenant-Id) here: a SuperAdmin token carries no tenantId
            // claim, and honouring a client-supplied header for that case let a
            // SuperAdmin-authenticated request read/write ANY tenant's data on every
            // [Authorize]-only controller just by setting X-Tenant-Id. Tenant-scoped
            // controllers are also restricted to HotelAdmin/Staff roles server-side
            // (see their [Authorize(Roles = ...)]) so SuperAdmin can't reach them at
            // all, but this stays claim-only regardless as the single source of truth.
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
            return claim != null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }

    public string TenantName => _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
}
