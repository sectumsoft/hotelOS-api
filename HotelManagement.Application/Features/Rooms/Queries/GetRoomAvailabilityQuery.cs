using MediatR;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Domain.Enums;

namespace HotelManagement.Application.Features.Rooms.Queries;

public record GetRoomAvailabilityQuery(DateTime Date) : IRequest<List<RoomAvailabilityDto>>;

public class RoomAvailabilityDto
{
    public Guid RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }

    // optional (nice UX)
    public string? GuestName { get; set; }
}

public class GetRoomAvailabilityQueryHandler
    : IRequestHandler<GetRoomAvailabilityQuery, List<RoomAvailabilityDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;

    public GetRoomAvailabilityQueryHandler(
        IApplicationDbContext context,
        ITenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    public async Task<List<RoomAvailabilityDto>> Handle(GetRoomAvailabilityQuery request, CancellationToken ct)
    {
        var date = request.Date.Date;
        var tenantId = _tenantService.TenantId;

        var rooms = await _context.Rooms
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .Select(r => new RoomAvailabilityDto
            {
                RoomId = r.Id,
                RoomNumber = r.RoomNumber,

                // 🔥 CORE LOGIC
                IsAvailable = !_context.Bookings.Any(b =>
                    b.RoomId == r.Id &&
                    b.TenantId == tenantId &&
                    b.Status != BookingStatus.Cancelled &&
                    b.Status != BookingStatus.CheckedOut &&
                    date >= b.CheckInDate &&
                    date < b.CheckOutDate
                ),

                // optional: show guest name if booked
                GuestName = _context.Bookings
                    .Where(b =>
                        b.RoomId == r.Id &&
                        b.TenantId == tenantId &&
                        b.Status != BookingStatus.Cancelled &&
                        b.Status != BookingStatus.CheckedOut &&
                        date >= b.CheckInDate &&
                        date < b.CheckOutDate
                    )
                    .Select(b => b.GuestName)
                    .FirstOrDefault()
            })
            .OrderBy(r => r.RoomNumber)
            .ToListAsync(ct);

        return rooms;
    }
}