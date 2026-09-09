using MediatR;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Domain.Enums;

namespace HotelManagement.Application.Features.Bookings.Commands;

public record UpdateBookingCommand(
    Guid Id,
    string GuestName,
    string GuestPhone,
    string? GuestAddress,
    Guid RoomId,
    DateTime CheckInDate,
    DateTime CheckOutDate,
    int NumberOfGuests,
    bool AdvancePaid,
    decimal AdvanceAmount) : IRequest<bool>;

public class UpdateBookingCommandHandler : IRequestHandler<UpdateBookingCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly INotificationRecorder _notify;

    public UpdateBookingCommandHandler(IApplicationDbContext ctx, ITenantService ts, INotificationRecorder notify)
    {
        _context = ctx;
        _tenantService = ts;
        _notify = notify;
    }

    public async Task<bool> Handle(UpdateBookingCommand req, CancellationToken ct)
    {
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == req.Id && b.TenantId == _tenantService.TenantId, ct);
        if (booking == null) return false;

        // Once a guest has checked in (or the stay is over / cancelled) the booking
        // is locked — cancel and re-book instead.
        if (booking.Status != BookingStatus.Confirmed)
            throw new Exception("Only upcoming (Confirmed) bookings can be edited");

        if (req.NumberOfGuests <= 0)
            throw new Exception("Number of guests must be at least 1");

        var room = await _context.Rooms.FirstOrDefaultAsync(
            r => r.Id == req.RoomId && r.TenantId == _tenantService.TenantId, ct)
            ?? throw new Exception("Room not found");

        var checkIn = DateTime.SpecifyKind(req.CheckInDate.Date, DateTimeKind.Utc);
        var checkOut = DateTime.SpecifyKind(req.CheckOutDate.Date, DateTimeKind.Utc);

        var nights = (int)(checkOut - checkIn).TotalDays;
        if (nights <= 0)
            throw new Exception("Check-out date must be after check-in date");

        var clash = await _context.Bookings.AnyAsync(b =>
            b.Id != booking.Id &&
            b.RoomId == req.RoomId &&
            b.TenantId == _tenantService.TenantId &&
            b.Status != BookingStatus.Cancelled &&
            b.Status != BookingStatus.CheckedOut &&
            b.CheckInDate < checkOut && checkIn < b.CheckOutDate, ct);
        if (clash)
            throw new Exception("That room is already booked for the selected dates");

        var total = nights * room.PricePerNight;
        var advance = req.AdvancePaid ? Math.Max(0, req.AdvanceAmount) : 0m;
        if (advance > total)
            throw new Exception("Advance amount cannot exceed the total booking amount");

        booking.GuestName = req.GuestName;
        booking.GuestPhone = req.GuestPhone;
        booking.GuestAddress = req.GuestAddress;
        booking.RoomId = req.RoomId;
        booking.CheckInDate = checkIn;
        booking.CheckOutDate = checkOut;
        booking.TotalNights = nights;
        booking.NumberOfGuests = req.NumberOfGuests;
        booking.TotalAmount = total;
        booking.AdvancePaid = advance > 0;
        booking.AdvanceAmount = advance;
        booking.BalanceAmount = total - advance;
        booking.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        await _notify.RecordAsync("booking-updated", "Booking updated",
            $"{booking.BookingNumber} · {booking.GuestName}", "/bookings", booking.Id.ToString(), ct);
        return true;
    }
}
