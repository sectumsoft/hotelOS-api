using Amazon.S3;
using Amazon.S3.Model;
using HotelManagement.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace HotelManagement.Infrastructure.Services;

/// <summary>
/// Stores images in Cloudflare R2 (S3-compatible) instead of the local disk, so uploads survive
/// backend redeploys on hosts with an ephemeral filesystem (e.g. Render's free tier).
/// </summary>
public class R2ImageService : IImageService
{
    private readonly IAmazonS3 _s3;
    private readonly ObjectStorageOptions _opts;

    public R2ImageService(IOptions<ObjectStorageOptions> opts)
    {
        _opts = opts.Value;
        _s3 = new AmazonS3Client(_opts.AccessKey, _opts.SecretKey, new AmazonS3Config
        {
            ServiceURL = $"https://{_opts.AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true,
        });
    }

    public async Task<string> SaveImageAsync(Stream imageStream, string fileName, string folder)
    {
        var key = $"{folder}/{Guid.NewGuid()}_{Path.GetFileName(fileName)}";

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _opts.BucketName,
            Key = key,
            InputStream = imageStream,
            ContentType = GetContentType(fileName),
            DisablePayloadSigning = true, // R2 rejects the chunked/streaming signature the SDK uses by default.
        });

        return $"{_opts.PublicBaseUrl.TrimEnd('/')}/{key}";
    }

    public async Task DeleteImageAsync(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return;

        var prefix = _opts.PublicBaseUrl.TrimEnd('/') + "/";
        var key = imageUrl.StartsWith(prefix) ? imageUrl[prefix.Length..] : imageUrl.TrimStart('/');
        if (string.IsNullOrEmpty(key)) return;

        await _s3.DeleteObjectAsync(_opts.BucketName, key);
    }

    private static string GetContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => "image/jpeg",
    };
}
