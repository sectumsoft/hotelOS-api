using MediatR;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Application.Common.Mappings;

namespace HotelManagement.Application.Features.Bookings.Queries;

public record GetBookingByIdQuery(Guid Id) : IRequest<BookingDto?>;

public class GetBookingByIdQueryHandler : IRequestHandler<GetBookingByIdQuery, BookingDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;

    public GetBookingByIdQueryHandler(IApplicationDbContext ctx, ITenantService ts)
    { _context = ctx; _tenantService = ts; }

    public async Task<BookingDto?> Handle(GetBookingByIdQuery req, CancellationToken ct)
    {
        var booking = await _context.Bookings
            .Include(b => b.Room)
            .FirstOrDefaultAsync(b => b.Id == req.Id
                && b.TenantId == _tenantService.TenantId
                && !b.IsDeleted, ct);

        return booking?.ToDto();
    }
}
