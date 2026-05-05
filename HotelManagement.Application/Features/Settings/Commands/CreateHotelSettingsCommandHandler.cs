using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HotelManagement.Application.Features.Settings.Commands
{
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

            // 🔴 Prevent duplicate settings per tenant
            var existing = await _context.HotelSettings
                .FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);

            if (existing != null)
                throw new Exception("Settings already exist for this hotel");

            var settings = new HotelSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                HotelName = request.HotelName,
                Subdomain = request.Subdomain,
                Email = request.Email,
                Phone = request.Phone,
                Address = request.Address
            };

            _context.HotelSettings.Add(settings);

            await _context.SaveChangesAsync(ct);

            return settings.Id;
        }
    }
}
