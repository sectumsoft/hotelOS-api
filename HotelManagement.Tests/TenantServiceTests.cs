using System.Security.Claims;
using HotelManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace HotelManagement.Tests;

/// <summary>
/// Regression coverage for the cross-tenant bypass found in the VAPT pass:
/// TenantService.TenantId used to fall back to a client-supplied X-Tenant-Id
/// header whenever the JWT carried no tenantId claim (true for every SuperAdmin
/// token), letting a SuperAdmin-authenticated request read/write any tenant's
/// data on the [Authorize]-only controllers just by setting that header.
/// </summary>
public class TenantServiceTests
{
    private static TenantService NewService(HttpContext ctx)
    {
        var accessor = new HttpContextAccessor { HttpContext = ctx };
        return new TenantService(accessor);
    }

    private static HttpContext ContextWithClaims(params Claim[] claims)
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        return ctx;
    }

    [Fact]
    public void Resolves_the_tenant_from_the_jwt_claim()
    {
        var tenantId = Guid.NewGuid();
        var ctx = ContextWithClaims(new Claim("tenantId", tenantId.ToString()));

        Assert.Equal(tenantId, NewService(ctx).TenantId);
    }

    [Fact]
    public void Never_trusts_an_X_Tenant_Id_header_when_the_token_has_no_claim()
    {
        // Simulates a SuperAdmin token, which carries no tenantId claim.
        var ctx = ContextWithClaims(new Claim(ClaimTypes.Role, "SuperAdmin"));
        ctx.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();

        Assert.Equal(Guid.Empty, NewService(ctx).TenantId);
    }

    [Fact]
    public void A_forged_header_cannot_override_the_tokens_own_tenant_claim()
    {
        var realTenant = Guid.NewGuid();
        var ctx = ContextWithClaims(new Claim("tenantId", realTenant.ToString()));
        ctx.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString(); // someone else's tenant

        Assert.Equal(realTenant, NewService(ctx).TenantId);
    }
}
