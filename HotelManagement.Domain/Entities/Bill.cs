using HotelManagement.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace HotelManagement.Domain.Entities;

public class Bill : BaseEntity
{
    public string BillNumber { get; set; } = string.Empty;
    public Guid BookingId { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public string? Notes { get; set; }
    public Booking Booking { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
    public ICollection<BillItem> Items { get; set; } = new List<BillItem>();
}

public class BillItem : BaseEntity
{
    public Guid BillId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Room, Service, Tax, Discount
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal Amount { get; set; }
    public Bill Bill { get; set; } = null!;
}
