namespace Nexus.Services.Products;

public static class ProductSkuHelper
{
    public static string GenerateSku(string productSlug, IEnumerable<string> optionValues, string? skuPrefix = null)
    {
        var prefix = string.IsNullOrWhiteSpace(skuPrefix)
            ? productSlug.Trim()
            : skuPrefix.Trim();

        var segments = new List<string> { ProductSlugHelper.GenerateSlug(prefix) };
        segments.AddRange(optionValues.Select(v => ProductSlugHelper.GenerateSlug(v)).Where(s => !string.IsNullOrWhiteSpace(s)));

        var sku = string.Join("-", segments.Where(s => !string.IsNullOrWhiteSpace(s)));
        return sku.Length <= 50 ? sku : sku[..50].TrimEnd('-');
    }
}
