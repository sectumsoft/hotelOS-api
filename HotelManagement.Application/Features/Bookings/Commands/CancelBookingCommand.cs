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
    private readonly INotificationRecorder _notify;

    public CancelBookingCommandHandler(IApplicationDbContext ctx, ITenantService ts, INotificationRecorder notify)
    { _context = ctx; _tenantService = ts; _notify = notify; }

    public async Task<bool> Handle(CancelBookingCommand req, CancellationToken ct)
    {
        var booking = await _context.Bookings.Include(b => b.Room)
            .FirstOrDefaultAsync(b => b.Id == req.BookingId && b.TenantId == _tenantService.TenantId, ct);
        if (booking == null) return false;

        // Only release the room if this booking actually had someone in it.
        // A future (Confirmed) booking never occupied the room, so cancelling it
        // must not flip a room that another guest is currently checked into.
        if (booking.Status == BookingStatus.CheckedIn && booking.Room != null)
            booking.Room.Status = RoomStatus.Available;

        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        await _notify.RecordAsync("booking-cancelled", "Booking cancelled",
            $"{booking.BookingNumber} · {booking.GuestName}", "/bookings", booking.Id.ToString(), ct);
        return true;
    }
}
