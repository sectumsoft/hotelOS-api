using HotelManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<User> Users { get; }
    DbSet<Room> Rooms { get; }
    DbSet<RoomType> RoomTypes { get; }
    DbSet<RoomImage> RoomImages { get; }
    DbSet<Amenity> Amenities { get; }
    DbSet<RoomAmenity> RoomAmenities { get; }
    DbSet<Guest> Guests { get; }
    DbSet<Booking> Bookings { get; }
    DbSet<Payment> Payments { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    DbSet<Bill> Bills { get; }
    DbSet<BillItem> BillItems { get; }
    DbSet<HotelSettings> HotelSettings { get; }
    DbSet<Notification> Notifications { get; }
}
