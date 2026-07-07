namespace Nexus.Services.Products.Models;

public sealed class VariantUpsertInput
{
    public int? Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public bool IsActive { get; set; } = true;

    public string? ImageUrl { get; set; }

    public IReadOnlyList<int> OptionValueIds { get; set; } = [];
}

public sealed class UpsertVariantsRequest
{
    public IReadOnlyList<VariantUpsertInput> Variants { get; set; } = [];

    public IReadOnlyList<int> DeleteVariantIds { get; set; } = [];
}
