using MediatR;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Domain.Enums;

namespace HotelManagement.Application.Features.Bookings.Commands;

public record CheckOutCommand(Guid BookingId) : IRequest<bool>;

public class CheckOutCommandHandler : IRequestHandler<CheckOutCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly INotificationRecorder _notify;

    public CheckOutCommandHandler(IApplicationDbContext ctx, ITenantService ts, INotificationRecorder notify)
    { _context = ctx; _tenantService = ts; _notify = notify; }

    public async Task<bool> Handle(CheckOutCommand req, CancellationToken ct)
    {
        var booking = await _context.Bookings.Include(b => b.Room)
            .FirstOrDefaultAsync(b => b.Id == req.BookingId && b.TenantId == _tenantService.TenantId, ct);
        if (booking == null) return false;
        booking.Status = BookingStatus.CheckedOut;
        booking.Room.Status = RoomStatus.Available;
        booking.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        await _notify.RecordAsync("check-out", "Guest checked out",
            $"{booking.GuestName} · Room {booking.Room?.RoomNumber}", "/bookings", booking.Id.ToString(), ct);
        return true;
    }
}
