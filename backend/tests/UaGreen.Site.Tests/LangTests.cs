using UaGreen.Site.Api.Features;

namespace UaGreen.Site.Tests;

public class LangTests
{
    [Fact]
    public void Czech_falls_back_to_english_then_ukrainian()
    {
        Assert.Equal("cs", Lang.Norm("CS"));
        Assert.Equal("uk", Lang.Norm("de"));
        Assert.Equal("uk", Lang.Norm(null));
        Assert.Equal(["cs", "en", "uk"], Lang.Chain("cs"));
        Assert.Equal("EN", Lang.Pick("UK", "EN", "cs"));
        Assert.Equal("UK", Lang.Pick("UK", " ", "cs"));
        Assert.Equal("UK", Lang.Pick("UK", "EN", "uk"));
        Assert.Equal("CS", Lang.Pick("UK", "EN", "cs", "CS"));
        Assert.Equal("EN", Lang.Pick("UK", "EN", "cs", " "));
        Assert.Equal("EN", Lang.Pick("UK", "EN", "en", "CS"));
    }
}
