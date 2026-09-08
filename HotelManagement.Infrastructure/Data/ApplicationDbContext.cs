using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<RoomImage> RoomImages => Set<RoomImage>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<RoomAmenity> RoomAmenities => Set<RoomAmenity>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillItem> BillItems => Set<BillItem>();
    public DbSet<HotelSettings> HotelSettings => Set<HotelSettings>();
    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        mb.Entity<Tenant>(e => { e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(200).IsRequired(); e.Property(x => x.Subdomain).HasMaxLength(100).IsRequired(); e.HasIndex(x => x.Subdomain).IsUnique(); });

        mb.Entity<User>(e => { e.HasKey(x => x.Id); e.Property(x => x.Email).HasMaxLength(300).IsRequired(); e.HasIndex(x => new { x.Email, x.TenantId }).IsUnique(); e.HasOne(x => x.Tenant).WithMany(t => t.Users).HasForeignKey(x => x.TenantId); });

        mb.Entity<Room>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.RoomNumber).HasMaxLength(20).IsRequired();
            e.Property(x => x.PricePerNight).HasColumnType("decimal(18,2)");
            e.HasIndex(x => new { x.RoomNumber, x.TenantId }).IsUnique();
            e.HasOne(x => x.Tenant).WithMany(t => t.Rooms).HasForeignKey(x => x.TenantId);
        });

        mb.Entity<RoomAmenity>(e => { e.HasKey(x => new { x.RoomId, x.AmenityId }); e.HasOne(x => x.Room).WithMany(r => r.RoomAmenities).HasForeignKey(x => x.RoomId); e.HasOne(x => x.Amenity).WithMany(a => a.RoomAmenities).HasForeignKey(x => x.AmenityId); });

        mb.Entity<RoomType>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(60).IsRequired();
            e.HasIndex(x => new { x.Name, x.TenantId }).IsUnique();
        });

        mb.Entity<Booking>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.AdvanceAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.BalanceAmount).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.Room).WithMany(r => r.Bookings).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Guest).WithMany(g => g.Bookings).HasForeignKey(x => x.GuestId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Tenant).WithMany(t => t.Bookings).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.NoAction);
        });

        mb.Entity<Payment>(e => { e.HasKey(x => x.Id); e.Property(x => x.Amount).HasColumnType("decimal(18,2)"); });
    }
}
