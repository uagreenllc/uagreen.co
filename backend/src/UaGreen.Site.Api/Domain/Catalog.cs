namespace UaGreen.Site.Api.Domain;

public sealed class Category
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string NameUk { get; set; } = "";
    public string? NameEn { get; set; }
    public int Sort { get; set; }
    public string? ImageKey { get; set; }
    public List<Product> Products { get; set; } = [];
}

public sealed class Product
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string? Sku { get; set; }
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    public string NameUk { get; set; } = "";
    public string? NameEn { get; set; }
    public string? ShortDescUk { get; set; }
    public string? ShortDescEn { get; set; }
    public string? DescriptionHtmlUk { get; set; }
    public string? DescriptionHtmlEn { get; set; }
    public string? SpecsHtmlUk { get; set; }
    public string? SpecsHtmlEn { get; set; }
    public string? FaqHtmlUk { get; set; }
    public string? FaqHtmlEn { get; set; }

    public decimal Price { get; set; }
    public decimal? OldPrice { get; set; }
    public bool InStock { get; set; } = true;
    public bool IsFeatured { get; set; }
    public int Sort { get; set; }

    public string? SeoTitleUk { get; set; }
    public string? SeoTitleEn { get; set; }
    public string? SeoDescUk { get; set; }
    public string? SeoDescEn { get; set; }

    public bool Published { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<ProductImage> Images { get; set; } = [];
    public List<ProductFile> Files { get; set; } = [];
    public List<ProductOption> Options { get; set; } = [];
}

public sealed class ProductImage
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Key { get; set; } = "";
    public string? Alt { get; set; }
    public int Sort { get; set; }
}

public sealed class ProductFile
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
}

/// A dropdown on the product page (e.g. "Щогла"); each value may change the price.
public sealed class ProductOption
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string NameUk { get; set; } = "";
    public string? NameEn { get; set; }
    public int Sort { get; set; }
    public List<ProductOptionValue> Values { get; set; } = [];
}

public sealed class ProductOptionValue
{
    public Guid Id { get; set; }
    public Guid OptionId { get; set; }
    public ProductOption? Option { get; set; }
    public string ValueUk { get; set; } = "";
    public string? ValueEn { get; set; }
    public decimal PriceDelta { get; set; }
    public int Sort { get; set; }
}
