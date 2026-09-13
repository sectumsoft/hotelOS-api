using System.Text.Json;
using HotelManagement.API.Models;

namespace HotelManagement.Tests;

/// <summary>
/// Regression coverage for a real production bug: a cleared "Tax (%)" / "Discount"
/// input on the frontend serializes as JSON `null`. System.Text.Json rejects
/// `null` for a non-nullable `decimal`, which failed the *whole* request body —
/// the client saw a raw 400 ("The request field is required") with no bill
/// created and no useful error message. TaxPercent/DiscountAmount are nullable
/// on the request DTO specifically so this deserializes instead of throwing.
/// </summary>
public class GenerateBillRequestTests
{
    [Fact]
    public void Null_tax_and_discount_deserialize_instead_of_throwing()
    {
        var json = """{"extraServices":[],"discountAmount":null,"taxPercent":null,"notes":null}""";

        var request = JsonSerializer.Deserialize<GenerateBillRequest>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(request);
        Assert.Null(request!.TaxPercent);
        Assert.Null(request.DiscountAmount);
    }

    [Fact]
    public void Omitted_tax_and_discount_default_to_zero()
    {
        var json = """{"extraServices":[]}""";

        var request = JsonSerializer.Deserialize<GenerateBillRequest>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(request);
        Assert.Equal(0m, request!.TaxPercent);
        Assert.Equal(0m, request.DiscountAmount);
    }
}
