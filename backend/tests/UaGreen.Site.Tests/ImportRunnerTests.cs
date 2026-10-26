using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UaGreen.Site.Api.Infrastructure.Import;
using UaGreen.Site.Api.Infrastructure.Persistence;

namespace UaGreen.Site.Tests;

public class ImportRunnerTests
{
    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase("import-" + Guid.NewGuid()).Options);

    private static string FixtureDir => Path.Combine(AppContext.BaseDirectory, "Fixtures", "migration");

    [Fact]
    public async Task Imports_fixture_and_uploads_media_once()
    {
        await using var db = Db();
        var uploaded = new List<string>();
        Task Put(string key, Stream s, string ct, CancellationToken _) { uploaded.Add(key + "|" + ct); return Task.CompletedTask; }
        Task<bool> Exists(string key, CancellationToken _) => Task.FromResult(key == "already.jpg");

        await ImportRunner.ImportAsync(db, FixtureDir, Put, Exists, NullLogger.Instance, CancellationToken.None);

        var cat = Assert.Single(db.Categories);
        Assert.Equal("vitroelektrostanciyi", cat.Slug);

        var p = await db.Products.Include(x => x.Images).Include(x => x.Options).ThenInclude(o => o.Values).SingleAsync();
        Assert.Equal("k1v4", p.Slug);
        Assert.Equal(cat.Id, p.CategoryId);
        Assert.Equal(47000m, p.Price);
        Assert.Equal(73000m, p.OldPrice);
        Assert.True(p.IsFeatured);
        Assert.Equal(2, p.Images.Count);
        var opt = Assert.Single(p.Options);
        Assert.Equal("Щогла", opt.NameUk);
        Assert.Equal(5000m, opt.Values.Single(v => v.ValueUk == "9 м").PriceDelta);

        Assert.Equal("yak-pravilno-vstanoviti-machtu", (await db.BlogPosts.SingleAsync()).Slug);
        Assert.Equal("junior-assembly-fitter", (await db.Vacancies.SingleAsync()).Slug);
        Assert.Equal(2, await db.Content.CountAsync());
        Assert.Equal("https://www.instagram.com/uagreenllc", (await db.Settings.SingleAsync(s => s.Key == "socials.instagram")).Value);
        Assert.Equal("+380668696072,+380957173772", (await db.Settings.SingleAsync(s => s.Key == "phones")).Value);
        Assert.Equal("/vacancies/junior-assembly-fitter", (await db.Redirects.SingleAsync(r => r.FromPath == "/junior-assembly-fitter")).ToPath);

        Assert.Equal(["img1.jpg|image/jpeg"], uploaded); // already.jpg skipped

        // Idempotent: a second run changes nothing.
        await ImportRunner.ImportAsync(db, FixtureDir, Put, Exists, NullLogger.Instance, CancellationToken.None);
        Assert.Equal(1, await db.Products.CountAsync());
        Assert.Equal(1, await db.Categories.CountAsync());
    }
}
