namespace CureFlow.Application.Common;

public static class TenantSlugHelper
{
    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "api", "www", "platform", "app", "mail", "support", "static", "assets",
    };

    public const int MaxSlugLength = 63;

    public static string Slugify(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');

    public static bool IsReserved(string slug) => ReservedSlugs.Contains(slug);

    /// <summary>Appends -2, -3, … when base slug is taken; keeps total length within DNS limits.</summary>
    public static string WithSuffix(string baseSlug, int suffix)
    {
        if (suffix <= 1)
            return Truncate(baseSlug, MaxSlugLength);

        var suffixPart = $"-{suffix}";
        var maxBase = MaxSlugLength - suffixPart.Length;
        return Truncate(baseSlug, maxBase) + suffixPart;
    }

    private static string Truncate(string slug, int maxLength) =>
        slug.Length <= maxLength ? slug : slug[..maxLength].TrimEnd('-');
}
