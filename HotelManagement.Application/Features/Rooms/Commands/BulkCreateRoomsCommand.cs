using MediatR;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Domain.Entities;
using HotelManagement.Domain.Enums;

namespace HotelManagement.Application.Features.Rooms.Commands;

/// <summary>One parsed row from the uploaded spreadsheet.</summary>
public record BulkRoomRow(
    int Row,
    string? RoomNumber,
    string? RoomType,
    decimal? PricePerNight,
    string? Description,
    string? Status,
    List<string>? Amenities);

public record BulkCreateRoomsCommand(List<BulkRoomRow> Rows) : IRequest<BulkCreateRoomsResult>;

public class BulkRoomError
{
    public int Row { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class BulkCreateRoomsResult
{
    public int Added { get; set; }
    public List<BulkRoomError> Skipped { get; set; } = new();
}

public class BulkCreateRoomsCommandHandler : IRequestHandler<BulkCreateRoomsCommand, BulkCreateRoomsResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;

    public BulkCreateRoomsCommandHandler(IApplicationDbContext context, ITenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    public async Task<BulkCreateRoomsResult> Handle(BulkCreateRoomsCommand request, CancellationToken ct)
    {
        var tenantId = _tenantService.TenantId;
        var result = new BulkCreateRoomsResult();

        // Existing room numbers for this hotel (case-insensitive), to reject duplicates.
        var existingNumbers = await _context.Rooms
            .Where(r => r.TenantId == tenantId && !r.IsDeleted)
            .Select(r => r.RoomNumber)
            .ToListAsync(ct);
        var existing = new HashSet<string>(existingNumbers, StringComparer.OrdinalIgnoreCase);

        // Tenant amenities cache (name -> entity), reused / extended across rows.
        var amenityList = await _context.Amenities
            .Where(a => a.TenantId == tenantId)
            .ToListAsync(ct);
        var amenityCache = new Dictionary<string, Amenity>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in amenityList) amenityCache[a.Name] = a;

        // Room types defined for this hotel (case-insensitive), for validation.
        var typeNames = await RoomTypeResolver.GetNamesAsync(_context, tenantId, ct);
        var typeLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in typeNames) typeLookup[n] = n;

        var seenInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toAdd = new List<Room>();

        foreach (var row in request.Rows)
        {
            var number = row.RoomNumber?.Trim();
            if (string.IsNullOrWhiteSpace(number))
            {
                result.Skipped.Add(new BulkRoomError { Row = row.Row, RoomNumber = "", Reason = "Room number is required" });
                continue;
            }

            if (!typeLookup.TryGetValue(row.RoomType?.Trim() ?? string.Empty, out var roomType))
            {
                result.Skipped.Add(new BulkRoomError { Row = row.Row, RoomNumber = number, Reason = $"Unknown room type '{row.RoomType}' - use one of: {string.Join(", ", typeNames)}" });
                continue;
            }

            var statusRaw = string.IsNullOrWhiteSpace(row.Status) ? "Available" : row.Status.Trim();
            if (!Enum.TryParse<RoomStatus>(statusRaw, ignoreCase: true, out var status))
            {
                result.Skipped.Add(new BulkRoomError { Row = row.Row, RoomNumber = number, Reason = $"Invalid status '{row.Status}' - use Available, Occupied or Maintenance" });
                continue;
            }

            if (row.PricePerNight is not > 0)
            {
                result.Skipped.Add(new BulkRoomError { Row = row.Row, RoomNumber = number, Reason = "Price per night must be greater than 0" });
                continue;
            }

            if (existing.Contains(number))
            {
                result.Skipped.Add(new BulkRoomError { Row = row.Row, RoomNumber = number, Reason = "A room with this number already exists" });
                continue;
            }

            if (!seenInBatch.Add(number))
            {
                result.Skipped.Add(new BulkRoomError { Row = row.Row, RoomNumber = number, Reason = "Duplicate room number in the file" });
                continue;
            }

            var room = new Room
            {
                TenantId = tenantId,
                RoomNumber = number,
                RoomType = roomType,
                PricePerNight = row.PricePerNight!.Value,
                Description = string.IsNullOrWhiteSpace(row.Description) ? null : row.Description.Trim(),
                Status = status
            };

            var amenityNames = (row.Amenities ?? new List<string>())
                .Select(a => a?.Trim() ?? string.Empty)
                .Where(a => a.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var amenityName in amenityNames)
            {
                if (!amenityCache.TryGetValue(amenityName, out var amenity))
                {
                    amenity = new Amenity { TenantId = tenantId, Name = amenityName, Icon = "bi-check" };
                    _context.Amenities.Add(amenity);
                    amenityCache[amenityName] = amenity;
                }
                room.RoomAmenities.Add(new RoomAmenity { RoomId = room.Id, AmenityId = amenity.Id });
            }

            toAdd.Add(room);
        }

        if (toAdd.Count > 0)
        {
            _context.Rooms.AddRange(toAdd);
            await _context.SaveChangesAsync(ct);
        }

        result.Added = toAdd.Count;
        return result;
    }
}
