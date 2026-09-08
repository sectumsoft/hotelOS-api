using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Application.Features.Settings.Commands;

public class CreateHotelSettingsCommandHandler
    : IRequestHandler<CreateHotelSettingsCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;

    public CreateHotelSettingsCommandHandler(
        IApplicationDbContext context,
        ITenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    public async Task<Guid> Handle(CreateHotelSettingsCommand request, CancellationToken ct)
    {
        var tenantId = _tenantService.TenantId;

        if (string.IsNullOrWhiteSpace(request.HotelName))
            throw new Exception("Hotel name is required");

        var settings = await _context.HotelSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);

        if (settings == null)
        {
            settings = new HotelSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                // Subdomain is the hotel's identity — owned by the SuperAdmin, not editable here.
                Subdomain = string.Empty
            };
            _context.HotelSettings.Add(settings);
        }

        settings.HotelName = request.HotelName.Trim();
        settings.Email = request.Email?.Trim() ?? string.Empty;
        settings.Phone = request.Phone?.Trim() ?? string.Empty;
        settings.Address = request.Address?.Trim() ?? string.Empty;
        settings.UpdatedAt = DateTime.UtcNow;

        // Keep the Tenant name (shown in the sidebar/topbar) in sync.
        var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant != null)
        {
            settings.Subdomain = tenant.Subdomain;
            tenant.Name = settings.HotelName;
        }

        await _context.SaveChangesAsync(ct);
        return settings.Id;
    }
}
