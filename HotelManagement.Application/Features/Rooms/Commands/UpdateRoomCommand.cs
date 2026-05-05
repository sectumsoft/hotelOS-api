using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Domain.Entities;
using HotelManagement.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Application.Features.Rooms.Commands;

public record UpdateRoomCommand(
    Guid Id, string RoomNumber, string RoomType, decimal PricePerNight,
    string? Description, string Status, List<string> Amenities, List<string> ImageUrls, List<string> DeleteImageUrls) : IRequest<bool>;

public class UpdateRoomCommandHandler : IRequestHandler<UpdateRoomCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly IImageService _imageService;

    public UpdateRoomCommandHandler(IApplicationDbContext context, ITenantService tenantService, IImageService imageService)
    { _context = context; _tenantService = tenantService; _imageService = imageService; }

    public async Task<bool> Handle(UpdateRoomCommand request, CancellationToken ct)
    {
        var room = await _context.Rooms
            .Include(r => r.RoomAmenities)
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.TenantId == _tenantService.TenantId, ct);

        if (room == null) return false;

        room.RoomNumber = request.RoomNumber;
        room.RoomType = Enum.Parse<RoomType>(request.RoomType);
        room.PricePerNight = request.PricePerNight;
        room.Description = request.Description;
        room.Status = Enum.Parse<RoomStatus>(request.Status);
        room.UpdatedAt = DateTime.UtcNow;

        // 1. sync amenities — use DbSet directly instead of Clear()
        var existingAmenities = room.RoomAmenities.ToList();
        _context.RoomAmenities.RemoveRange(existingAmenities);

        foreach (var amenityName in request.Amenities)
        {
            var amenity = _context.Amenities
                .FirstOrDefault(a => a.Name == amenityName && a.TenantId == _tenantService.TenantId);
            if (amenity == null)
            {
                amenity = new Amenity { TenantId = _tenantService.TenantId, Name = amenityName, Icon = "bi-check" };
                _context.Amenities.Add(amenity);
            }
            _context.RoomAmenities.Add(new RoomAmenity { RoomId = room.Id, AmenityId = amenity.Id });
        }

        // 2. delete removed images — use DbSet directly instead of room.Images.Remove()
        foreach (var urlToDelete in request.DeleteImageUrls)
        {
            var image = room.Images.FirstOrDefault(i => i.ImageUrl == urlToDelete);
            if (image == null) continue;
            await _imageService.DeleteImageAsync(urlToDelete);
            _context.RoomImages.Remove(image);  // ← use DbSet directly
        }

        // 3. add new images
        bool hasPrimary = room.Images.Any(i => i.IsPrimary && !request.DeleteImageUrls.Contains(i.ImageUrl));
        for (int i = 0; i < request.ImageUrls.Count; i++)
        {
            _context.RoomImages.Add(new RoomImage
            {
                RoomId = room.Id,
                ImageUrl = request.ImageUrls[i],
                IsPrimary = !hasPrimary && i == 0
            });
            hasPrimary = true;
        }

        await _context.SaveChangesAsync(ct);
        return true;
    }
}
