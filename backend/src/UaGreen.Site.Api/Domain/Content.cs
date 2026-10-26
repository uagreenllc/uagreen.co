namespace UaGreen.Site.Api.Domain;

public sealed class BlogPost
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string TitleUk { get; set; } = "";
    public string? TitleEn { get; set; }
    public string? ExcerptUk { get; set; }
    public string? ExcerptEn { get; set; }
    public string BodyHtmlUk { get; set; } = "";
    public string? BodyHtmlEn { get; set; }
    public string? CoverKey { get; set; }
    public string? Author { get; set; }
    public DateTime PublishedAt { get; set; }
    public string? SeoTitleUk { get; set; }
    public string? SeoTitleEn { get; set; }
    public string? SeoDescUk { get; set; }
    public string? SeoDescEn { get; set; }
    public bool Published { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}

public sealed class Vacancy
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string TitleUk { get; set; } = "";
    public string? TitleEn { get; set; }
    /// One of the tabs on /vacancies: Менеджмент / Слюсарі / Інженери / Програмування.
    public string Department { get; set; } = "";
    public string EmploymentType { get; set; } = "";
    public string City { get; set; } = "Львів";
    public string? Experience { get; set; }
    public string? Language { get; set; }
    public string? RequirementsHtmlUk { get; set; }
    public string? RequirementsHtmlEn { get; set; }
    public string? NiceToHaveHtmlUk { get; set; }
    public string? NiceToHaveHtmlEn { get; set; }
    public string? ConditionsHtmlUk { get; set; }
    public string? ConditionsHtmlEn { get; set; }
    public bool IsActive { get; set; } = true;
    public int Sort { get; set; }
    public DateTime PublishedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// Editable page / home-section text. Key like "page.about" or "home.hero"; one row per lang.
public sealed class ContentBlock
{
    public string Key { get; set; } = "";
    public string Lang { get; set; } = "uk";
    public string? Title { get; set; }
    public string BodyHtml { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}

/// Public site settings (phones, socials, analytics ids). Never secrets — those live in env.
public sealed class Setting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

/// Old Weblium path → new path (301).
public sealed class Redirect
{
    public string FromPath { get; set; } = "";
    public string ToPath { get; set; } = "";
}
