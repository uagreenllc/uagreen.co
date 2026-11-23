using System.Xml.Linq;
using UaGreen.Site.Api.Domain;
using UaGreen.Site.Api.Features.Catalog;

namespace UaGreen.Site.Tests;

public class FeedTests
{
    static readonly XNamespace G = "http://base.google.com/ns/1.0";

    [Fact]
    public void Feeds_carry_price_sale_category_and_absolute_media()
    {
        var cat = new Category { Id = Guid.NewGuid(), NameUk = "Вітрогенератори" };
        var p = new Product
        {
            Id = Guid.NewGuid(), Slug = "k5", Sku = "606", NameUk = "Вітрогенератор 5кВт", CategoryId = cat.Id, Category = cat,
            Price = 15000m, OldPrice = 18000m, InStock = false,
            DescriptionHtmlUk = "<p>Тихий &amp; надійний</p><img src=\"/media/a.jpg\">",
            Images = [new ProductImage { Key = "p/2.jpg", Sort = 2 }, new ProductImage { Key = "p/1.jpg", Sort = 1 }],
        };
        const string b = "https://uagreen.co";

        var prom = XDocument.Parse(FeedEndpoints.Prom([p], [cat], b, DateTime.UtcNow).ToString());
        var offer = prom.Descendants("offer").Single();
        Assert.Equal("606", (string?)offer.Attribute("id"));
        Assert.Equal("false", (string?)offer.Attribute("available"));
        Assert.Equal("15000", (string?)offer.Element("price"));
        Assert.Equal("18000", (string?)offer.Element("oldprice"));
        Assert.Equal((string?)prom.Descendants("category").Single().Attribute("id"), (string?)offer.Element("categoryId"));
        Assert.Equal(["https://uagreen.co/media/p/1.jpg", "https://uagreen.co/media/p/2.jpg"], offer.Elements("picture").Select(e => e.Value));
        Assert.Contains("src=\"https://uagreen.co/media/a.jpg\"", offer.Element("description_ua")!.Value);

        var item = FeedEndpoints.Google([p], b).Descendants("item").Single();
        Assert.Equal("18000.00 UAH", (string?)item.Element(G + "price"));
        Assert.Equal("15000.00 UAH", (string?)item.Element(G + "sale_price"));
        Assert.Equal("out_of_stock", (string?)item.Element(G + "availability"));
        Assert.Equal("Тихий & надійний", (string?)item.Element(G + "description"));
        Assert.Equal("https://uagreen.co/shop/k5", (string?)item.Element(G + "link"));
        Assert.Equal("https://uagreen.co/media/p/1.jpg", (string?)item.Element(G + "image_link"));
        Assert.Equal("606", (string?)item.Element(G + "mpn"));
    }
}
