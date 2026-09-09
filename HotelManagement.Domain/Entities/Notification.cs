using HotelManagement.Domain.Common;

namespace HotelManagement.Domain.Entities;

/// <summary>An activity item shown in the header bell — one row per meaningful event.</summary>
public class Notification : BaseEntity
{
    public string Type { get; set; } = string.Empty;   // room-created, booking-created, check-in, check-out, booking-cancelled, rooms-imported, bill-generated, staff-added
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Link { get; set; }                  // route to open on click, e.g. "/bookings"
    public string? EntityId { get; set; }
    public string? ActorName { get; set; }              // who triggered it
    public bool IsRead { get; set; }
}
