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

    public CheckInCommandHandler(IApplicationDbContext ctx, ITenantService ts)
    {
        _context = ctx;
        _tenantService = ts;
    }

    public async Task<bool> Handle(CheckInCommand req, CancellationToken ct)
    {
        var booking = await _context.Bookings
            .Include(b => b.Room)
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
            var primaryGuest = req.Guests.First();

            booking.GuestName = primaryGuest.Name;
            booking.GuestPhone = primaryGuest.Phone;
            booking.GuestAddress = primaryGuest.Address;
        }

        if (req.Guests != null && req.Guests.Any())
        {
            foreach (var guestDto in req.Guests)
            {
                var existingGuest = await _context.Guests.FirstOrDefaultAsync(
                    g => g.Phone == guestDto.Phone &&
                         g.TenantId == _tenantService.TenantId,
                    ct
                );

                if (existingGuest == null)
                {
                    var newGuest = new Guest
                    {
                        TenantId = _tenantService.TenantId,

                        Name = guestDto.Name,
                        Phone = guestDto.Phone,
                        Address = guestDto.Address,
                        IdProofType = guestDto.IdProofType,
                        IdProofNumber = guestDto.IdProofNumber,
                        IdProofUrl = guestDto.IdProofUrl,
                        BookingId = booking.Id,
                        TotalStays = 1
                    };
                    _context.Guests.Add(newGuest);
                }
                else
                {
                    existingGuest.Name = guestDto.Name;
                    existingGuest.Address = guestDto.Address;

                    if (!string.IsNullOrWhiteSpace(guestDto.IdProofType))
                        existingGuest.IdProofType = guestDto.IdProofType;

                    if (!string.IsNullOrWhiteSpace(guestDto.IdProofNumber))
                        existingGuest.IdProofNumber = guestDto.IdProofNumber;
                    if (!string.IsNullOrWhiteSpace(guestDto.IdProofUrl))
                        existingGuest.IdProofUrl = guestDto.IdProofUrl;
                    existingGuest.BookingId = booking.Id;
                    existingGuest.TotalStays += 1;
                }
            }
        }

        booking.Status = BookingStatus.CheckedIn;
        booking.Room.Status = RoomStatus.Occupied;
        booking.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return true;
    }
}