using MediatR;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;

namespace HotelManagement.Application.Features.Rooms.Commands;

public record DeleteRoomCommand(Guid Id) : IRequest<bool>;

public class DeleteRoomCommandHandler : IRequestHandler<DeleteRoomCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;

    public DeleteRoomCommandHandler(IApplicationDbContext context, ITenantService tenantService)
    { _context = context; _tenantService = tenantService; }

    public async Task<bool> Handle(DeleteRoomCommand request, CancellationToken ct)
    {
        var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == request.Id && r.TenantId == _tenantService.TenantId, ct);
        if (room == null) return false;
        room.IsDeleted = true;
        room.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
