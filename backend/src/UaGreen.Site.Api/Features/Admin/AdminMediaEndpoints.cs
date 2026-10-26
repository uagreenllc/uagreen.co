using UaGreen.Site.Api.Common;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.RateLimiting;
using UaGreen.Site.Api.Infrastructure.Storage;

namespace UaGreen.Site.Api.Features.Admin;

/// Admin uploads: multipart `file` + `folder`; the returned key goes into the
/// product/post/category. Public URL is `/media/<key>` (served by nginx).
public static class AdminMediaEndpoints
{
    private static readonly HashSet<string> Folders = ["products", "categories", "blog", "content", "files"];
    private const long MaxImageBytes = 10L * 1024 * 1024;
    private const long MaxFileBytes = 25L * 1024 * 1024;

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/media", async (HttpRequest request, FileStorage storage, CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var folder = form["folder"].ToString();
            if (!Folders.Contains(folder)) throw ProblemException.BadRequest("folder must be one of: " + string.Join(", ", Folders));
            var file = form.Files.GetFile("file") ?? throw ProblemException.BadRequest("file is required");
            var isDoc = folder == "files";
            var ok = isDoc ? FileStorage.DocumentTypes.Contains(file.ContentType) : FileStorage.ImageTypes.Contains(file.ContentType);
            if (!ok) throw ProblemException.BadRequest(isDoc ? "Only PDF/Word files" : "Only JPEG/PNG/WebP/GIF/SVG images");
            if (file.Length > (isDoc ? MaxFileBytes : MaxImageBytes)) throw ProblemException.BadRequest("File too large");

            await using var s = file.OpenReadStream();
            var key = await storage.SaveAsync(folder, file.ContentType, s, ct);
            return Results.Ok(new { key, url = FileStorage.Url(key) });
        }).RequireAuthorization(Policies.SalesOrEditor).RequireRateLimiting(RateLimitingExtensions.Uploads).DisableAntiforgery();

        app.MapDelete("/api/admin/media/{*key}", (string key, FileStorage storage) =>
        {
            storage.Delete(key);
            return Results.NoContent();
        }).RequireAuthorization(Policies.SalesOrEditor);
    }
}
