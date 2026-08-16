using Nexus.Services.Products.Models;

namespace Nexus.Services.Products;

public static class VariantResolutionHelper
{
    public static string GetOptionValueSignature(IEnumerable<int> optionValueIds) =>
        string.Join(",", optionValueIds.OrderBy(id => id));

    public static ProductVariantDto? ResolveFromVariants(
        IReadOnlyList<ProductVariantDto> variants,
        IReadOnlyList<int> optionValueIds,
        int optionCount)
    {
        if (optionCount == 0)
        {
            return variants.Count == 1 ? variants[0] : null;
        }

        if (optionValueIds.Count != optionCount)
            return null;

        var signature = GetOptionValueSignature(optionValueIds);
        return variants.FirstOrDefault(v =>
            GetOptionValueSignature(v.OptionValueIds) == signature);
    }

    public static bool IsValidSelection(
        IReadOnlyList<ProductOptionDto> options,
        IReadOnlyList<int> optionValueIds)
    {
        if (options.Count == 0)
            return optionValueIds.Count == 0;

        if (optionValueIds.Count != options.Count)
            return false;

        var allValueIds = options.SelectMany(o => o.Values).Select(v => v.Id).ToHashSet();
        if (optionValueIds.Any(id => !allValueIds.Contains(id)))
            return false;

        var selectedValues = optionValueIds
            .Select(id => options.SelectMany(o => o.Values).FirstOrDefault(v => v.Id == id))
            .Where(v => v is not null)
            .ToList();

        if (selectedValues.Count != options.Count)
            return false;

        return selectedValues.Select(v => v!.Id).Distinct().Count() == options.Count
            && selectedValues
                .Select(v => options.First(o => o.Values.Any(ov => ov.Id == v!.Id)).Id)
                .Distinct()
                .Count() == options.Count;
    }

    public static ProductVariantDto? SelectDefaultVariant(IReadOnlyList<ProductVariantDto> variants)
    {
        if (variants.Count == 0)
            return null;

        var ordered = variants.OrderBy(v => v.Id).ToList();
        return ordered.FirstOrDefault(v => v.StockQuantity > 0) ?? ordered[0];
    }

    public static Dictionary<int, int> BuildSelectedValues(
        IReadOnlyList<ProductOptionDto> options,
        ProductVariantDto variant)
    {
        var selected = new Dictionary<int, int>();
        var valueIds = variant.OptionValueIds.ToHashSet();

        foreach (var option in options)
        {
            var match = option.Values.FirstOrDefault(v => valueIds.Contains(v.Id));
            if (match is not null)
                selected[option.Id] = match.Id;
        }

        return selected;
    }
}
