using MediatR;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;

namespace HotelManagement.Application.Features.Settings.Queries;

public record GetHotelSettingsQuery() : IRequest<HotelSettingsDto?>;

public class HotelSettingsDto
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }

    public string HotelName { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class GetHotelSettingsQueryHandler
    : IRequestHandler<GetHotelSettingsQuery, HotelSettingsDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;

    public GetHotelSettingsQueryHandler(
        IApplicationDbContext context,
        ITenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    public async Task<HotelSettingsDto?> Handle(GetHotelSettingsQuery request, CancellationToken ct)
    {
        var tenantId = _tenantService.TenantId;

        var settings = await _context.HotelSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);

        if (settings != null)
        {
            return new HotelSettingsDto
            {
                Id = settings.Id,
                TenantId = settings.TenantId,
                HotelName = settings.HotelName,
                Subdomain = settings.Subdomain,
                Email = settings.Email,
                Phone = settings.Phone,
                Address = settings.Address,
                CreatedAt = settings.CreatedAt,
                UpdatedAt = settings.UpdatedAt
            };
        }

        // No settings row yet — seed the view from what the SuperAdmin created
        // when onboarding this hotel (the Tenant record).
        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

        if (tenant == null) return null;

        return new HotelSettingsDto
        {
            TenantId = tenant.Id,
            HotelName = tenant.Name,
            Subdomain = tenant.Subdomain,
            Email = string.Empty,
            Phone = string.Empty,
            Address = string.Empty
        };
    }
}
