using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.RateLimiting;
using UaGreen.Site.Api.Infrastructure.Storage;

namespace UaGreen.Site.Api.Features.Admin;

public sealed record PresignRequest(string Folder, string ContentType);

/// The admin uploads straight to MinIO with a presigned PUT; the returned key goes
/// into the product/post/category. Public URL is `/media/<key>`.
public static class AdminMediaEndpoints
{
    private static readonly HashSet<string> Folders = ["products", "categories", "blog", "content", "files"];

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/media/presign", async (PresignRequest req, FileStorage storage) =>
        {
            if (!Folders.Contains(req.Folder ?? "")) throw ProblemException.BadRequest("folder must be one of: " + string.Join(", ", Folders));
            var isDoc = req.Folder == "files";
            var ok = isDoc ? FileStorage.DocumentTypes.Contains(req.ContentType ?? "") : FileStorage.ImageTypes.Contains(req.ContentType ?? "");
            if (!ok) throw ProblemException.BadRequest(isDoc ? "Only PDF/Word files" : "Only JPEG/PNG/WebP/GIF/SVG images");
            var r = await storage.PresignUploadAsync(req.Folder!, req.ContentType!, TimeSpan.FromMinutes(5));
            return Results.Ok(new { uploadUrl = r.UploadUrl, key = r.Key, url = r.Url, expiresIn = 300 });
        }).RequireAuthorization(Policies.SalesOrEditor).RequireRateLimiting(RateLimitingExtensions.Presign);
    }
}
