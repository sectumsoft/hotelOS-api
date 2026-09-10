using HotelManagement.Application.Features.Bookings.Queries;
using HotelManagement.Application.Features.Rooms.Queries;
using HotelManagement.Domain.Entities;

namespace HotelManagement.Application.Common.Mappings;

/// <summary>
/// Hand-written entity → DTO projections. Replaces AutoMapper: the mappings here
/// are trivial and there were only two of them, so a static helper is clearer than
/// a runtime mapping engine (and drops a dependency flagged NU1903 / now commercial).
/// </summary>
public static class DtoMappings
{
    public static RoomImageDto ToDto(this RoomImage i) => new()
    {
        Id = i.Id,
        RoomId = i.RoomId,
        ImageUrl = i.ImageUrl,
        IsPrimary = i.IsPrimary,
    };

    public static RoomDto ToDto(this Room r) => new()
    {
        Id = r.Id,
        TenantId = r.TenantId ?? Guid.Empty,
        RoomNumber = r.RoomNumber,
        RoomType = r.RoomType,
        PricePerNight = r.PricePerNight,
        Description = r.Description,
        Status = r.Status.ToString(),
        Amenities = r.RoomAmenities?.Select(ra => ra.Amenity.Name).ToList() ?? new(),
        Images = r.Images?.Select(img => img.ToDto()).ToList() ?? new(),
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };

    public static BookingDto ToDto(this Booking b) => new()
    {
        Id = b.Id,
        BookingNumber = b.BookingNumber,
        TenantId = b.TenantId ?? Guid.Empty,
        GuestId = b.GuestId,
        GuestName = b.GuestName,
        GuestPhone = b.GuestPhone,
        GuestAddress = b.GuestAddress,
        RoomId = b.RoomId,
        RoomNumber = b.Room?.RoomNumber ?? string.Empty,
        RoomType = b.Room?.RoomType ?? string.Empty,
        CheckInDate = b.CheckInDate,
        CheckOutDate = b.CheckOutDate,
        TotalNights = b.TotalNights,
        NumberOfGuests = b.NumberOfGuests,
        TotalAmount = b.TotalAmount,
        AdvancePaid = b.AdvancePaid,
        AdvanceAmount = b.AdvanceAmount,
        BalanceAmount = b.BalanceAmount,
        Status = b.Status.ToString(),
        CreatedAt = b.CreatedAt,
        UpdatedAt = b.UpdatedAt,
    };
}
