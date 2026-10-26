using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace UaGreen.Site.Api.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public string Driver { get; set; } = "S3";
    public string S3Bucket { get; set; } = "media";
    /// Not world-readable: CVs from job applications. Read via short presigned GETs only.
    public string S3PrivateBucket { get; set; } = "private";
    public string S3Region { get; set; } = "us-east-1";
    public string S3AccessKey { get; set; } = "";
    public string S3SecretKey { get; set; } = "";
    public string? S3ServiceUrl { get; set; }
    /// Browser-reachable endpoint used ONLY for presigned URLs (the public origin;
    /// nginx proxies /<bucket>/ to MinIO with the Host header preserved).
    public string? S3PublicServiceUrl { get; set; }
    public bool S3ForcePathStyle { get; set; } = true;
}

public sealed record PresignUploadResult(string UploadUrl, string Key, string Url);

/// MinIO/S3 object store. Keys are public: the bucket is world-readable and nginx
/// serves `/media/<key>` — so `Url(key)` is just that relative path.
public sealed class FileStorage(IOptions<FileStorageOptions> opts, IAmazonS3 s3, FileStorage.PresignClient presign)
{
    public sealed record PresignClient(IAmazonS3 Client);

    private static readonly Dictionary<string, string> Ext = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp", ["image/gif"] = ".gif",
        ["image/svg+xml"] = ".svg", ["application/pdf"] = ".pdf",
        ["application/msword"] = ".doc",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
    };

    public static readonly HashSet<string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "image/webp", "image/gif", "image/svg+xml" };
    public static readonly HashSet<string> DocumentTypes = new(StringComparer.OrdinalIgnoreCase)
        { "application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" };

    private readonly FileStorageOptions _opts = opts.Value;

    public static string Url(string key) => "/media/" + key;

    public static string NewKey(string folder, string contentType)
        => $"{folder}/{Guid.NewGuid():N}{(Ext.TryGetValue(contentType, out var e) ? e : "")}";

    public async Task<PresignUploadResult> PresignUploadAsync(string folder, string contentType, TimeSpan expiry)
    {
        var key = NewKey(folder, contentType);
        var url = await presign.Client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _opts.S3Bucket,
            Key = key,
            Expires = DateTime.UtcNow.Add(expiry),
            Verb = HttpVerb.PUT,
            ContentType = contentType,
        });
        return new PresignUploadResult(url, key, Url(key));
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _opts.S3Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
        }, ct);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct)
    {
        try
        {
            await s3.GetObjectMetadataAsync(_opts.S3Bucket, key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task DeleteAsync(string key, CancellationToken ct) => s3.DeleteObjectAsync(_opts.S3Bucket, key, ct);

    // ── Private bucket (job-application CVs) ──
    public async Task PutPrivateAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _opts.S3PrivateBucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
        }, ct);
    }

    public Task<string> PresignPrivateDownloadAsync(string key, TimeSpan expiry)
        => presign.Client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _opts.S3PrivateBucket,
            Key = key,
            Expires = DateTime.UtcNow.Add(expiry),
            Verb = HttpVerb.GET,
        });

    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<FileStorageOptions>>().Value;
            return CreateClient(o, o.S3ServiceUrl);
        });
        services.AddSingleton(sp =>
        {
            var o = sp.GetRequiredService<IOptions<FileStorageOptions>>().Value;
            return new PresignClient(string.IsNullOrEmpty(o.S3PublicServiceUrl)
                ? sp.GetRequiredService<IAmazonS3>()
                : CreateClient(o, o.S3PublicServiceUrl));
        });
        services.AddSingleton<FileStorage>();
    }

    private static AmazonS3Client CreateClient(FileStorageOptions o, string? serviceUrl)
    {
        var cfg = new AmazonS3Config { ForcePathStyle = o.S3ForcePathStyle };
        if (!string.IsNullOrEmpty(serviceUrl)) cfg.ServiceURL = serviceUrl;
        else cfg.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(o.S3Region);
        return new AmazonS3Client(new BasicAWSCredentials(o.S3AccessKey, o.S3SecretKey), cfg);
    }
}
