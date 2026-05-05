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
        var settings = await _context.HotelSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == _tenantService.TenantId, ct);

        if (settings == null) return null;

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
}