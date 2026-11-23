using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Infrastructure.Auth;
using UaGreen.Site.Api.Infrastructure.Persistence;
using UaGreen.Site.Api.Infrastructure.Storage;

namespace UaGreen.Site.Api.Features.Catalog;

/// Канали збуту: product feeds the channels pull on their own schedule — Prom.ua (YML) and
/// Google Merchant Center (RSS 2.0 + g:), which Facebook/Instagram Commerce Manager reads as is.
/// Ukrainian copy, UAH, published products only.
/// ponytail: option price deltas aren't exported as variants; add item_group_id rows if a channel asks for them.
public static class FeedEndpoints
{
    const string Shop = "UA Green";
    static readonly XNamespace G = "http://base.google.com/ns/1.0";

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/feeds").AllowAnonymous().CacheOutput("public");
        g.MapGet("/prom.xml", async (AppDbContext db, IOptions<AppOptions> o, CancellationToken ct) =>
            Xml(Prom(await LoadAsync(db, ct), await db.Categories.AsNoTracking().OrderBy(c => c.Sort).ToListAsync(ct),
                Base(o), DateTime.UtcNow)));
        g.MapGet("/google.xml", async (AppDbContext db, IOptions<AppOptions> o, CancellationToken ct) =>
            Xml(Google(await LoadAsync(db, ct), Base(o))));

        // For the admin page: the public URLs to paste into each channel.
        app.MapGet("/api/admin/feeds", (IOptions<AppOptions> o) =>
                new { prom = $"{Base(o)}/api/feeds/prom.xml", google = $"{Base(o)}/api/feeds/google.xml" })
            .RequireAuthorization(Policies.Sales);
    }

    public static XDocument Prom(IReadOnlyList<Product> products, IReadOnlyList<Category> cats, string baseUrl, DateTime now) => new(
        new XElement("yml_catalog", new XAttribute("date", now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
            new XElement("shop",
                new XElement("name", Shop), new XElement("company", Shop), new XElement("url", baseUrl),
                new XElement("currencies", new XElement("currency", new XAttribute("id", "UAH"), new XAttribute("rate", "1"))),
                new XElement("categories", cats.Select(c => new XElement("category", new XAttribute("id", CatId(c.Id)), c.NameUk))),
                new XElement("offers", products.Select(p => new XElement("offer",
                    new XAttribute("id", OfferId(p)), new XAttribute("available", p.InStock ? "true" : "false"),
                    new XElement("url", $"{baseUrl}/shop/{p.Slug}"),
                    new XElement("price", Num(p.Price)),
                    p.OldPrice > p.Price ? new XElement("oldprice", Num(p.OldPrice.Value)) : null,
                    new XElement("currencyId", "UAH"),
                    p.CategoryId is { } cid ? new XElement("categoryId", CatId(cid)) : null,
                    Pictures(p, baseUrl).Select(u => new XElement("picture", u)),
                    new XElement("name", p.NameUk), new XElement("name_ua", p.NameUk),
                    new XElement("vendor", Shop),
                    string.IsNullOrWhiteSpace(p.Sku) ? null : new XElement("vendorCode", p.Sku),
                    new XElement("description", new XCData(Html(p, baseUrl))),
                    new XElement("description_ua", new XCData(Html(p, baseUrl)))))))));

    public static XDocument Google(IReadOnlyList<Product> products, string baseUrl) => new(
        new XElement("rss", new XAttribute("version", "2.0"), new XAttribute(XNamespace.Xmlns + "g", G),
            new XElement("channel",
                new XElement("title", Shop), new XElement("link", baseUrl), new XElement("description", Shop),
                products.Select(p =>
                {
                    var pics = Pictures(p, baseUrl).ToList();
                    var sale = p.OldPrice > p.Price;
                    var text = Text(Html(p, baseUrl));
                    return new XElement("item",
                        new XElement(G + "id", OfferId(p)),
                        new XElement(G + "title", p.NameUk.Length > 150 ? p.NameUk[..150] : p.NameUk),
                        new XElement(G + "description", text.Length > 5000 ? text[..5000] : text),
                        new XElement(G + "link", $"{baseUrl}/shop/{p.Slug}"),
                        pics.Take(1).Select(u => new XElement(G + "image_link", u)),
                        pics.Skip(1).Take(10).Select(u => new XElement(G + "additional_image_link", u)),
                        new XElement(G + "availability", p.InStock ? "in_stock" : "out_of_stock"),
                        new XElement(G + "price", Money(sale ? p.OldPrice!.Value : p.Price)),
                        sale ? new XElement(G + "sale_price", Money(p.Price)) : null,
                        new XElement(G + "condition", "new"),
                        new XElement(G + "brand", Shop),
                        string.IsNullOrWhiteSpace(p.Sku) ? new XElement(G + "identifier_exists", "no") : new XElement(G + "mpn", p.Sku),
                        p.Category is null ? null : new XElement(G + "product_type", p.Category.NameUk));
                }))));

    static Task<List<Product>> LoadAsync(AppDbContext db, CancellationToken ct) =>
        db.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Images)
            .Where(p => p.Published).OrderBy(p => p.Sort).ThenBy(p => p.NameUk).ToListAsync(ct);

    static IResult Xml(XDocument doc) =>
        Results.Text("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + doc, "application/xml; charset=utf-8");

    static string Base(IOptions<AppOptions> o) => o.Value.PublicBaseUrl.TrimEnd('/');

    // Stable ids: the channels match updates by them. YML offer ids are ≤ 20 chars, category ids numeric.
    static string OfferId(Product p) => string.IsNullOrWhiteSpace(p.Sku) ? p.Id.ToString("N")[..20] : p.Sku.Trim();
    static uint CatId(Guid id) => BitConverter.ToUInt32(id.ToByteArray());

    static IEnumerable<string> Pictures(Product p, string baseUrl) =>
        p.Images.OrderBy(i => i.Sort).Select(i => baseUrl + FileStorage.Url(i.Key));

    // Site-relative /media/... links would be dead on the channel's side.
    static string Html(Product p, string baseUrl) =>
        (p.DescriptionHtmlUk ?? p.ShortDescUk ?? p.NameUk).Replace("src=\"/", $"src=\"{baseUrl}/");

    static string Text(string html) =>
        Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), @"\s+", " ").Trim();

    static string Num(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    static string Money(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture) + " UAH";
}
