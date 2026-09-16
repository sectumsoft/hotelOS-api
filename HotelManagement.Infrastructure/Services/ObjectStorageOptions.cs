namespace HotelManagement.Infrastructure.Services;

/// <summary>
/// Bound from the "ObjectStorage" config section. Provider "Local" (default) keeps writing to
/// wwwroot, which is fine for local dev but is wiped on every Render redeploy — set Provider to
/// "R2" plus the fields below (as env vars in hosted environments) to persist uploads instead.
/// </summary>
public class ObjectStorageOptions
{
    public string Provider { get; set; } = "Local";
    public string AccountId { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string BucketName { get; set; } = "";

    /// <summary>Public base URL for the bucket (R2.dev subdomain or a custom domain), no trailing slash.</summary>
    public string PublicBaseUrl { get; set; } = "";
}
