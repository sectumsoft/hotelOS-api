using HotelManagement.Domain.Common;
namespace HotelManagement.Domain.Entities;

public class Guest : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? IdProofType { get; set; }      
    public string? IdProofNumber { get; set; }  
    public string? IdProofUrl { get; set; }
    public int TotalStays { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }
}
