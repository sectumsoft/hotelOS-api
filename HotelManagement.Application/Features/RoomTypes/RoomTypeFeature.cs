using MediatR;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;
using DomainRoomType = HotelManagement.Domain.Entities.RoomType;

namespace HotelManagement.Application.Features.RoomTypes;

public class RoomTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int RoomCount { get; set; }
}

// ── List (lazily seeds the three defaults on first use) ───────────────────────
public record GetRoomTypesQuery() : IRequest<List<RoomTypeDto>>;

public class GetRoomTypesQueryHandler : IRequestHandler<GetRoomTypesQuery, List<RoomTypeDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantService _tenant;
    public GetRoomTypesQueryHandler(IApplicationDbContext db, ITenantService tenant) { _db = db; _tenant = tenant; }

    public async Task<List<RoomTypeDto>> Handle(GetRoomTypesQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;

        var types = await _db.RoomTypes.Where(t => t.TenantId == tenantId).ToListAsync(ct);

        if (types.Count == 0)
        {
            types = new[] { "Standard", "Deluxe", "Suite" }
                .Select(n => new DomainRoomType { TenantId = tenantId, Name = n })
                .ToList();
            _db.RoomTypes.AddRange(types);
            await _db.SaveChangesAsync(ct);
        }

        var counts = await _db.Rooms
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .GroupBy(r => r.RoomType)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return types
            .OrderBy(t => t.Name)
            .Select(t => new RoomTypeDto
            {
                Id = t.Id,
                Name = t.Name,
                RoomCount = counts.FirstOrDefault(c => c.Name == t.Name)?.Count ?? 0
            })
            .ToList();
    }
}

// ── Create or rename ─────────────────────────────────────────────────────────
public record SaveRoomTypeCommand(Guid? Id, string Name) : IRequest<Guid>;

public class SaveRoomTypeCommandHandler : IRequestHandler<SaveRoomTypeCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantService _tenant;
    public SaveRoomTypeCommandHandler(IApplicationDbContext db, ITenantService tenant) { _db = db; _tenant = tenant; }

    public async Task<Guid> Handle(SaveRoomTypeCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var name = request.Name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
            throw new Exception("Room type name is required");
        if (name.Length > 60)
            throw new Exception("Room type name is too long");

        var clash = await _db.RoomTypes.AnyAsync(t =>
            t.TenantId == tenantId &&
            t.Id != (request.Id ?? Guid.Empty) &&
            t.Name.ToLower() == name.ToLower(), ct);
        if (clash)
            throw new Exception($"A room type named '{name}' already exists");

        if (request.Id is Guid id)
        {
            var existing = await _db.RoomTypes.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, ct)
                ?? throw new Exception("Room type not found");

            var oldName = existing.Name;
            existing.Name = name;
            existing.UpdatedAt = DateTime.UtcNow;

            // Keep existing rooms pointing at the renamed type.
            if (!string.Equals(oldName, name, StringComparison.Ordinal))
            {
                var rooms = await _db.Rooms.Where(r => r.TenantId == tenantId && r.RoomType == oldName).ToListAsync(ct);
                foreach (var r in rooms) r.RoomType = name;
            }

            await _db.SaveChangesAsync(ct);
            return existing.Id;
        }

        var created = new DomainRoomType { TenantId = tenantId, Name = name };
        _db.RoomTypes.Add(created);
        await _db.SaveChangesAsync(ct);
        return created.Id;
    }
}

// ── Delete (blocked while rooms use it) ──────────────────────────────────────
public record DeleteRoomTypeCommand(Guid Id) : IRequest<bool>;

public class DeleteRoomTypeCommandHandler : IRequestHandler<DeleteRoomTypeCommand, bool>
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantService _tenant;
    public DeleteRoomTypeCommandHandler(IApplicationDbContext db, ITenantService tenant) { _db = db; _tenant = tenant; }

    public async Task<bool> Handle(DeleteRoomTypeCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var type = await _db.RoomTypes.FirstOrDefaultAsync(t => t.Id == request.Id && t.TenantId == tenantId, ct);
        if (type == null) return false;

        var inUse = await _db.Rooms.AnyAsync(r => r.TenantId == tenantId && !r.IsDeleted && r.RoomType == type.Name, ct);
        if (inUse)
            throw new Exception($"'{type.Name}' is used by one or more rooms — reassign those rooms first");

        _db.RoomTypes.Remove(type);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
