using HotelManagement.Domain.Common;

namespace HotelManagement.Domain.Entities;

/// <summary>A room category defined per hotel (e.g. Standard, Deluxe, Suite, Villa).</summary>
public class RoomType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Tenant Tenant { get; set; } = null!;
}
