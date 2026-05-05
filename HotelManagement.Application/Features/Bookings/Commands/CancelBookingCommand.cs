using MediatR;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Domain.Enums;

namespace HotelManagement.Application.Features.Bookings.Commands;

public record CancelBookingCommand(Guid BookingId) : IRequest<bool>;

public class CancelBookingCommandHandler : IRequestHandler<CancelBookingCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;

    public CancelBookingCommandHandler(IApplicationDbContext ctx, ITenantService ts)
    { _context = ctx; _tenantService = ts; }

    public async Task<bool> Handle(CancelBookingCommand req, CancellationToken ct)
    {
        var booking = await _context.Bookings.Include(b => b.Room)
            .FirstOrDefaultAsync(b => b.Id == req.BookingId && b.TenantId == _tenantService.TenantId, ct);
        if (booking == null) return false;
        booking.Status = BookingStatus.Cancelled;
        booking.Room.Status = RoomStatus.Available;
        booking.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
