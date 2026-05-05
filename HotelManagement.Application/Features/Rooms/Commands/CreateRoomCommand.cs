using MediatR;
using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Domain.Entities;
using HotelManagement.Domain.Enums;

namespace HotelManagement.Application.Features.Rooms.Commands;

public record CreateRoomCommand(
    string RoomNumber, string RoomType, decimal PricePerNight,
    string? Description, string Status, List<string> Amenities, List<string> ImageUrls) : IRequest<Guid>;

public class CreateRoomCommandHandler : IRequestHandler<CreateRoomCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;

    public CreateRoomCommandHandler(IApplicationDbContext context, ITenantService tenantService)
    { _context = context; _tenantService = tenantService; }

    public async Task<Guid> Handle(CreateRoomCommand request, CancellationToken ct)
    {
        var room = new Room
        {
            TenantId = _tenantService.TenantId,
            RoomNumber = request.RoomNumber,
            RoomType = Enum.Parse<RoomType>(request.RoomType),
            PricePerNight = request.PricePerNight,
            Description = request.Description,
            Status = Enum.Parse<RoomStatus>(request.Status)
        };

        foreach (var amenityName in request.Amenities)
        {
            var amenity = _context.Amenities.FirstOrDefault(a => a.Name == amenityName && a.TenantId == _tenantService.TenantId);
            if (amenity == null)
            {
                amenity = new Amenity { TenantId = _tenantService.TenantId, Name = amenityName, Icon = "bi-check" };
                _context.Amenities.Add(amenity);
            }
            room.RoomAmenities.Add(new RoomAmenity { RoomId = room.Id, AmenityId = amenity.Id });
        }
        for (int i = 0; i < request.ImageUrls.Count; i++)
        {
            room.Images.Add(new RoomImage
            {
                RoomId = room.Id,
                ImageUrl = request.ImageUrls[i],
                IsPrimary = i == 0   // first image is the primary/cover image
            });
        }
        _context.Rooms.Add(room);
        await _context.SaveChangesAsync(ct);
        return room.Id;
    }
}