using HotelManagement.Domain.Common;
using HotelManagement.Domain.Enums;
namespace HotelManagement.Domain.Entities;

public class Room : BaseEntity
{
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public string? Description { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public Tenant Tenant { get; set; } = null!;
    public ICollection<RoomImage> Images { get; set; } = new List<RoomImage>();
    public ICollection<RoomAmenity> RoomAmenities { get; set; } = new List<RoomAmenity>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public string? ImageUrl { get; set; }
}

public class RoomImage : BaseEntity
{
    public Guid RoomId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public Room Room { get; set; } = null!;
}

public class Amenity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public ICollection<RoomAmenity> RoomAmenities { get; set; } = new List<RoomAmenity>();
}

public class RoomAmenity
{
    public Guid RoomId { get; set; }
    public Guid AmenityId { get; set; }
    public Room Room { get; set; } = null!;
    public Amenity Amenity { get; set; } = null!;
}
