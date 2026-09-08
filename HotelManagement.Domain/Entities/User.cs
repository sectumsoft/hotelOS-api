using HotelManagement.Domain.Common;
using HotelManagement.Domain.Enums;
namespace HotelManagement.Domain.Entities;

public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Staff;
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Comma-separated module keys a Staff user may access (rooms,bookings,guests,reports). Ignored for admins.</summary>
    public string? Permissions { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public Tenant Tenant { get; set; } = null!;
}
