using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;
using DomainRoomType = HotelManagement.Domain.Entities.RoomType;

namespace HotelManagement.Application.Features.Rooms;

/// <summary>
/// Resolves a free-text room type against the tenant's defined room types
/// (case-insensitive) and returns the canonical name. Seeds the three defaults
/// the first time a hotel has none.
/// </summary>
public static class RoomTypeResolver
{
    public static async Task<List<string>> GetNamesAsync(IApplicationDbContext db, Guid tenantId, CancellationToken ct)
    {
        var names = await db.RoomTypes
            .Where(t => t.TenantId == tenantId)
            .Select(t => t.Name)
            .ToListAsync(ct);

        if (names.Count == 0)
        {
            names = new List<string> { "Standard", "Deluxe", "Suite" };
            db.RoomTypes.AddRange(names.Select(n => new DomainRoomType { TenantId = tenantId, Name = n }));
            await db.SaveChangesAsync(ct);
        }

        return names;
    }

    public static async Task<string> ResolveAsync(IApplicationDbContext db, Guid tenantId, string? input, CancellationToken ct)
    {
        var value = input?.Trim() ?? string.Empty;
        var names = await GetNamesAsync(db, tenantId, ct);
        var match = names.FirstOrDefault(n => string.Equals(n, value, StringComparison.OrdinalIgnoreCase));
        if (match == null)
            throw new Exception($"Unknown room type '{input}'. Add it under Settings → Room Types first.");
        return match;
    }
}
