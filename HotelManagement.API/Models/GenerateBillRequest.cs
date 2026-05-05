namespace HotelManagement.API.Models;

public class GenerateBillRequest
{
    public List<ServiceItemRequest> ExtraServices { get; set; } = new();
    public decimal DiscountAmount { get; set; } = 0;
    public decimal TaxPercent { get; set; } = 0;
    public string? Notes { get; set; }
}

public class ServiceItemRequest
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Quantity { get; set; } = 1;
}