using MediatR;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Domain.Entities;
using HotelManagement.Domain.Enums;

namespace HotelManagement.Application.Features.Bookings.Commands;
public class CheckInGuestDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? IdProofType { get; set; }
    public string? IdProofNumber { get; set; }
    public string? IdProofUrl { get; set; }
}

public record CheckInCommand(
    Guid BookingId,
    decimal AmountReceived,
    List<CheckInGuestDto> Guests
) : IRequest<bool>;

public class CheckInCommandHandler : IRequestHandler<CheckInCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly INotificationRecorder _notify;

    public CheckInCommandHandler(IApplicationDbContext ctx, ITenantService ts, INotificationRecorder notify)
    {
        _context = ctx;
        _tenantService = ts;
        _notify = notify;
    }

    public async Task<bool> Handle(CheckInCommand req, CancellationToken ct)
    {
        var booking = await _context.Bookings
            .Include(b => b.Room)
            .Include(b => b.Guest)
            .FirstOrDefaultAsync(
                b => b.Id == req.BookingId &&
                     b.TenantId == _tenantService.TenantId,
                ct
            );

        if (booking == null || booking.Status != BookingStatus.Confirmed)
            return false;

        booking.BalanceAmount = Math.Max(0, booking.BalanceAmount - req.AmountReceived);
        booking.NumberOfGuests = req.Guests?.Count ?? 0;

        if (req.Guests != null && req.Guests.Any())
        {
            // The check-in form's first entry is the account-holder for this
            // booking — the same person already on file as booking.Guest (set
            // when the booking was created). Update that SAME row instead of
            // searching/creating by phone: the check-in form has no phone field
            // at all, so every guest here arrives with Phone == "". Treating that
            // blank like a real lookup key either spawns a duplicate row for the
            // primary guest (their real ID proof lands on the duplicate, not the
            // profile Guest 360 actually reads) or — worse — collides two
            // unrelated guests from different bookings onto the same row, since
            // they'd all match on the same empty string.
            var primary = req.Guests.First();

            booking.GuestName = primary.Name;
            // Only overwrite what the check-in form actually collected. It doesn't
            // ask for phone/address, so blindly assigning primary.Phone/Address
            // here was wiping out the real values entered at booking time.
            if (!string.IsNullOrWhiteSpace(primary.Phone)) booking.GuestPhone = primary.Phone;
            if (!string.IsNullOrWhiteSpace(primary.Address)) booking.GuestAddress = primary.Address;

            if (booking.Guest != null)
            {
                booking.Guest.Name = primary.Name;
                if (!string.IsNullOrWhiteSpace(primary.Address)) booking.Guest.Address = primary.Address;
                if (!string.IsNullOrWhiteSpace(primary.IdProofType)) booking.Guest.IdProofType = primary.IdProofType;
                if (!string.IsNullOrWhiteSpace(primary.IdProofNumber)) booking.Guest.IdProofNumber = primary.IdProofNumber;
                if (!string.IsNullOrWhiteSpace(primary.IdProofUrl)) booking.Guest.IdProofUrl = primary.IdProofUrl;
            }

            // Everyone else is a companion captured for this stay only. Always
            // add a fresh row scoped to this booking — there's no reliable field
            // to dedupe an existing companion against (see above), so guessing
            // at a match risks overwriting an unrelated guest's record instead.
            foreach (var companion in req.Guests.Skip(1))
            {
                _context.Guests.Add(new Guest
                {
                    TenantId = _tenantService.TenantId,
                    Name = companion.Name,
                    Phone = companion.Phone ?? "",
                    Address = companion.Address,
                    IdProofType = companion.IdProofType,
                    IdProofNumber = companion.IdProofNumber,
                    IdProofUrl = companion.IdProofUrl,
                    BookingId = booking.Id,
                    TotalStays = 1
                });
            }
        }

        booking.Status = BookingStatus.CheckedIn;
        booking.Room.Status = RoomStatus.Occupied;
        booking.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        var partySize = req.Guests?.Count ?? 1;
        await _notify.RecordAsync("check-in", "Guest checked in",
            $"{booking.GuestName} · Room {booking.Room?.RoomNumber}" + (partySize > 1 ? $" · {partySize} guests" : ""),
            "/bookings", booking.Id.ToString(), ct);

        return true;
    }
}