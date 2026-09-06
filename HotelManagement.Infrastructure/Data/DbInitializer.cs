using HotelManagement.Domain.Entities;
using HotelManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        await context.Database.MigrateAsync();

        // ── Demo hotel + admin + staff + rooms (only on a fresh database) ──
        if (!await context.Tenants.AnyAsync())
        {
            var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Grand Hotel", Subdomain = "grandhotel", Plan = TenantPlan.Pro, IsActive = true };
            context.Tenants.Add(tenant);

            context.Users.Add(new User
            {
                TenantId = tenant.Id,
                Name = "Admin User",
                Email = "admin@grandhotel.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Role = UserRole.HotelAdmin,
                IsActive = true
            });

            context.Users.Add(new User
            {
                TenantId = tenant.Id,
                Name = "Front Desk",
                Email = "staff@grandhotel.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Role = UserRole.Staff,
                IsActive = true
            });

            var rooms = new[]
            {
                new Room { TenantId = tenant.Id, RoomNumber = "101", RoomType = RoomType.Standard, PricePerNight = 89, Status = RoomStatus.Available, Description = "Cozy standard room with garden view" },
                new Room { TenantId = tenant.Id, RoomNumber = "102", RoomType = RoomType.Standard, PricePerNight = 89, Status = RoomStatus.Occupied, Description = "Cozy standard room with garden view" },
                new Room { TenantId = tenant.Id, RoomNumber = "201", RoomType = RoomType.Deluxe, PricePerNight = 149, Status = RoomStatus.Available, Description = "Spacious deluxe room with city view" },
                new Room { TenantId = tenant.Id, RoomNumber = "202", RoomType = RoomType.Deluxe, PricePerNight = 149, Status = RoomStatus.Available, Description = "Spacious deluxe room with pool view" },
                new Room { TenantId = tenant.Id, RoomNumber = "301", RoomType = RoomType.Suite, PricePerNight = 299, Status = RoomStatus.Available, Description = "Luxury suite with panoramic views" },
                new Room { TenantId = tenant.Id, RoomNumber = "302", RoomType = RoomType.Suite, PricePerNight = 349, Status = RoomStatus.Maintenance, Description = "Presidential suite" },
            };
            context.Rooms.AddRange(rooms);

            await context.SaveChangesAsync();
        }

        // ── Platform SuperAdmin — always ensured, has no tenant ──
        if (!await context.Users.AnyAsync(u => u.Role == UserRole.SuperAdmin))
        {
            context.Users.Add(new User
            {
                TenantId = null,
                Name = "Platform Owner",
                Email = "superadmin@hotelos.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("superadmin123"),
                Role = UserRole.SuperAdmin,
                IsActive = true
            });

            await context.SaveChangesAsync();
        }
    }
}
