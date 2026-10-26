using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Common;

namespace UaGreen.Site.Api.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    /// Public media root; nginx serves it as `/media/<key>` (a Docker volume in prod).
    public string Root { get; set; } = "data/media";
    /// Job-application CVs. Never served by nginx; streamed to HR through the API.
    public string PrivateRoot { get; set; } = "data/private";
}

/// Plain files on disk. ponytail: MinIO/S3 dropped — the images are no longer
/// publicly pullable and a site with a few hundred photos does not need object
/// storage. Swap this class for an S3 driver if the media ever outgrows one volume.
public sealed class FileStorage
{
    private static readonly Dictionary<string, string> Ext = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp", ["image/gif"] = ".gif",
        ["image/svg+xml"] = ".svg", ["application/pdf"] = ".pdf",
        ["application/msword"] = ".doc",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
    };
    private static readonly Dictionary<string, string> TypeByExt =
        Ext.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    public static readonly HashSet<string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "image/webp", "image/gif", "image/svg+xml" };
    public static readonly HashSet<string> DocumentTypes = new(StringComparer.OrdinalIgnoreCase)
        { "application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" };

    private readonly string _root;
    private readonly string _private;

    public FileStorage(IOptions<FileStorageOptions> opts, IHostEnvironment env)
    {
        _root = Path.GetFullPath(opts.Value.Root, env.ContentRootPath);
        _private = Path.GetFullPath(opts.Value.PrivateRoot, env.ContentRootPath);
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_private);
    }

    public static string Url(string key) => "/media/" + key;

    public static string NewKey(string folder, string contentType)
        => $"{folder}/{Guid.NewGuid():N}{(Ext.TryGetValue(contentType, out var e) ? e : "")}";

    public static string ContentTypeFor(string key)
        => TypeByExt.TryGetValue(Path.GetExtension(key), out var t) ? t : "application/octet-stream";

    // ── Public media ──
    public async Task<string> SaveAsync(string folder, string contentType, Stream content, CancellationToken ct)
    {
        var key = NewKey(folder, contentType);
        await PutAsync(key, content, contentType, ct);
        return key;
    }

    /// Write at an exact key (the Weblium import keeps the original file names).
    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
        => await WriteAsync(Resolve(_root, key), content, ct);

    public Task<bool> ExistsAsync(string key, CancellationToken ct) => Task.FromResult(File.Exists(Resolve(_root, key)));

    public void Delete(string key)
    {
        var path = Resolve(_root, key);
        if (File.Exists(path)) File.Delete(path);
    }

    // ── Private files (CVs) ──
    public async Task<string> SavePrivateAsync(string folder, string contentType, Stream content, CancellationToken ct)
    {
        var key = NewKey(folder, contentType);
        await WriteAsync(Resolve(_private, key), content, ct);
        return key;
    }

    public Stream? OpenPrivate(string key)
    {
        var path = Resolve(_private, key);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public void DeletePrivate(string key)
    {
        var path = Resolve(_private, key);
        if (File.Exists(path)) File.Delete(path);
    }

    private static async Task WriteAsync(string path, Stream content, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = File.Create(path);
        await content.CopyToAsync(fs, ct);
    }

    /// Keys come from the DB or the admin; never let one escape the root.
    private static string Resolve(string root, string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Contains("..") || Path.IsPathRooted(key))
            throw ProblemException.BadRequest("Bad file key");
        var full = Path.GetFullPath(Path.Combine(root, key));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw ProblemException.BadRequest("Bad file key");
        return full;
    }
}
