namespace Nexus.Services.Products.Models;

public enum BulkVariantAction
{
    SetPrice,
    AdjustStock,
    SetActive
}

public sealed class BulkVariantActionRequest
{
    public IReadOnlyList<int> VariantIds { get; set; } = [];

    public BulkVariantAction Action { get; set; }

    public decimal? Price { get; set; }

    public int? StockDelta { get; set; }

    public bool? IsActive { get; set; }
}
