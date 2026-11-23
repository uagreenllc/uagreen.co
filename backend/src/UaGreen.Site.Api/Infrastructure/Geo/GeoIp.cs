using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Infrastructure.Storage;

namespace UaGreen.Site.Api.Infrastructure.Geo;

/// IP → ISO country code from the free DB-IP "IP to Country Lite" CSV (CC BY 4.0,
/// credited on the admin stats page). Downloaded into the private volume and
/// refreshed monthly; until the first load finishes every lookup returns null.
public sealed class GeoIp(IOptions<FileStorageOptions> opts, IHostEnvironment env, IHttpClientFactory http, ILogger<GeoIp> log)
    : BackgroundService
{
    private GeoTable? _table;
    private DateTime _loaded;

    public string? Country(IPAddress? ip) => ip is null ? null : _table?.Find(ip);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var file = Path.Combine(Path.GetFullPath(opts.Value.PrivateRoot, env.ContentRootPath), "geo", "dbip-country-lite.csv.gz");
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!File.Exists(file) || File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-35)) await DownloadAsync(file, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogWarning(e, "GeoIP download failed; keeping the old file");
            }
            try
            {
                if (File.Exists(file) && File.GetLastWriteTimeUtc(file) != _loaded)
                {
                    _loaded = File.GetLastWriteTimeUtc(file);
                    using var r = new StreamReader(new GZipStream(File.OpenRead(file), CompressionMode.Decompress));
                    _table = GeoTable.Load(r);
                    log.LogInformation("GeoIP loaded: {Rows} ranges", _table.Count);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogWarning(e, "GeoIP load failed");
            }
            await Task.Delay(TimeSpan.FromDays(1), ct);
        }
    }

    private async Task DownloadAsync(string file, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using var client = http.CreateClient();
        // This month's file appears on the 1st; fall back to last month's.
        foreach (var month in new[] { DateTime.UtcNow, DateTime.UtcNow.AddMonths(-1) })
        {
            using var res = await client.GetAsync($"https://download.db-ip.com/free/dbip-country-lite-{month:yyyy-MM}.csv.gz",
                HttpCompletionOption.ResponseHeadersRead, ct);
            if (!res.IsSuccessStatusCode) continue;
            var tmp = file + ".tmp";
            await using (var f = File.Create(tmp)) await res.Content.CopyToAsync(f, ct);
            File.Move(tmp, file, overwrite: true);
            return;
        }
        throw new HttpRequestException("DB-IP country file not found for this or last month");
    }
}

/// Sorted `start,end,CC` ranges, IPv4 mapped into IPv6 space; binary search per lookup.
/// ponytail: ~700k ranges in plain arrays (~30 MB); an mmdb reader if memory gets tight.
public sealed class GeoTable(UInt128[] starts, UInt128[] ends, string[] codes)
{
    public int Count => starts.Length;

    public static GeoTable Load(TextReader r)
    {
        var rows = new List<(UInt128 S, UInt128 E, string C)>();
        while (r.ReadLine() is { } line)
        {
            var p = line.Split(',');
            if (p.Length >= 3 && p[2].Length == 2 && p[2] != "ZZ" && IPAddress.TryParse(p[0], out var a) && IPAddress.TryParse(p[1], out var b))
                rows.Add((Num(a), Num(b), string.Intern(p[2])));
        }
        rows.Sort((x, y) => x.S.CompareTo(y.S));
        return new([.. rows.Select(x => x.S)], [.. rows.Select(x => x.E)], [.. rows.Select(x => x.C)]);
    }

    public string? Find(IPAddress ip)
    {
        var x = Num(ip);
        var i = Array.BinarySearch(starts, x);
        if (i < 0) i = ~i - 1;
        return i >= 0 && x <= ends[i] ? codes[i] : null;
    }

    private static UInt128 Num(IPAddress ip) => BinaryPrimitives.ReadUInt128BigEndian(ip.MapToIPv6().GetAddressBytes());
}
