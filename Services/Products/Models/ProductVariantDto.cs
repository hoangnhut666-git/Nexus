namespace Nexus.Services.Products.Models;

public sealed class ProductVariantDto
{
    public int Id { get; set; }

    public string Sku { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; }

    public IReadOnlyList<int> OptionValueIds { get; set; } = [];

    public string OptionLabel { get; set; } = string.Empty;
}
