using System.Text.RegularExpressions;

namespace UaGreen.Site.Api.Common;

public static partial class Slug
{
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Valid();

    public static bool IsValid(string? s) => !string.IsNullOrEmpty(s) && s.Length <= 120 && Valid().IsMatch(s);

    public static string Require(string? s)
        => IsValid(s) ? s! : throw ProblemException.BadRequest("slug must be lowercase latin letters, digits and dashes");
}
