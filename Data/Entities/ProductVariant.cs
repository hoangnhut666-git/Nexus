namespace Nexus.Data.Entities;

public class ProductVariant
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public Product Product { get; set; } = null!;

    public ICollection<VariantOptionValue> OptionValues { get; set; } = [];
}
