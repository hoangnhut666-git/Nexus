using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Nexus.Services.Categories;

public static partial class CategorySlugHelper
{
    public static string GenerateSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var normalized = name.Trim().ToLowerInvariant();
        normalized = normalized.Replace('đ', 'd').Replace('Đ', 'd');

        var decomposed = normalized.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        var slug = builder.ToString().Normalize(NormalizationForm.FormC);
        slug = NonAlphanumericRegex().Replace(slug, "");
        slug = WhitespaceRegex().Replace(slug, "-");
        slug = MultipleHyphenRegex().Replace(slug, "-");
        slug = slug.Trim('-');

        return slug;
    }

    [GeneratedRegex(@"[^a-z0-9\s-]", RegexOptions.Compiled)]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"-+", RegexOptions.Compiled)]
    private static partial Regex MultipleHyphenRegex();
}
