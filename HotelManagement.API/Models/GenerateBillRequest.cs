namespace HotelManagement.API.Models;

public class GenerateBillRequest
{
    public List<ServiceItemRequest> ExtraServices { get; set; } = new();

    // Nullable on purpose: a cleared number input serializes as JSON `null`, and
    // System.Text.Json rejects `null` for a non-nullable decimal by failing the
    // whole request body (the client saw this as a raw 400 with no bill created).
    // Treat "the field was left blank" the same as "0" instead of hard-erroring.
    public decimal? DiscountAmount { get; set; } = 0;
    public decimal? TaxPercent { get; set; } = 0;
    public string? Notes { get; set; }
}

public class ServiceItemRequest
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Quantity { get; set; } = 1;
}