using MediatR;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Domain.Entities;
using HotelManagement.Domain.Enums;

namespace HotelManagement.Application.Features.Bookings.Commands;
// Main booking command
public record CreateBookingCommand(
    string GuestName,
    string GuestPhone,
    string? GuestAddress,

    Guid RoomId,
    DateTime CheckInDate,
    DateTime CheckOutDate,

    int NumberOfGuests,

    bool AdvancePaid,
    decimal AdvanceAmount) : IRequest<Guid>;

public class CreateBookingCommandHandler : IRequestHandler<CreateBookingCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;

    public CreateBookingCommandHandler(IApplicationDbContext ctx, ITenantService ts)
    {
        _context = ctx;
        _tenantService = ts;
    }

    public async Task<Guid> Handle(CreateBookingCommand req, CancellationToken ct)
    {
        // Validate room
        var room = await _context.Rooms.FirstOrDefaultAsync(
            r => r.Id == req.RoomId && r.TenantId == _tenantService.TenantId,
            ct
        ) ?? throw new Exception("Room not found");

        // Basic validation
        if (req.NumberOfGuests <= 0)
            throw new Exception("Number of guests must be at least 1");

        // Normalise the incoming dates to UTC (an <input type="date"> value arrives
        // without a time zone) and drop any time component.
        var checkIn = DateTime.SpecifyKind(req.CheckInDate.Date, DateTimeKind.Utc);
        var checkOut = DateTime.SpecifyKind(req.CheckOutDate.Date, DateTimeKind.Utc);

        // Calculate booking totals
        var nights = (int)(checkOut - checkIn).TotalDays;

        if (nights <= 0)
            throw new Exception("Check-out date must be after check-in date");

        var total = nights * room.PricePerNight;

        var advance = req.AdvancePaid ? Math.Max(0, req.AdvanceAmount) : 0m;
        if (advance > total)
            throw new Exception("Advance amount cannot exceed the total booking amount");

        var balance = total - advance;

        // MAIN GUEST: Find or create
        var mainGuest = await _context.Guests.FirstOrDefaultAsync(
            g => g.Phone == req.GuestPhone && g.TenantId == _tenantService.TenantId,
            ct
        );

        if (mainGuest == null)
        {
            mainGuest = new Guest
            {
                TenantId = _tenantService.TenantId,
                Name = req.GuestName,
                Phone = req.GuestPhone,
                Address = req.GuestAddress,
                TotalStays = 1
            };

            _context.Guests.Add(mainGuest);
        }
        else
        {
            mainGuest.TotalStays += 1;
        }

        // Save now so GuestId is generated
        await _context.SaveChangesAsync(ct);

        // BOOKING NUMBER
        var bookingCount = await _context.Bookings.CountAsync(
            b => b.TenantId == _tenantService.TenantId,
            ct
        );

        // CREATE BOOKING
        var booking = new Booking
        {
            TenantId = _tenantService.TenantId,
            BookingNumber = $"BK-{(bookingCount + 1):D4}",

            GuestId = mainGuest.Id,
            GuestName = req.GuestName,
            GuestPhone = req.GuestPhone,
            GuestAddress = req.GuestAddress,

            RoomId = req.RoomId,
            CheckInDate = checkIn,
            CheckOutDate = checkOut,

            TotalNights = nights,
            TotalAmount = total,

            AdvancePaid = advance > 0,
            AdvanceAmount = advance,
            BalanceAmount = balance,

            Status = BookingStatus.Confirmed,

            NumberOfGuests = req.NumberOfGuests
        };

        _context.Bookings.Add(booking);

        // Update room status
        room.Status = RoomStatus.Occupied;

        // Final save
        await _context.SaveChangesAsync(ct);

        return booking.Id;
    }
}