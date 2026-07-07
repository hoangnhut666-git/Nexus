namespace Nexus.Data.Entities;

public class CartItem
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int ProductVariantId { get; set; }

    public int Quantity { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ProductVariant ProductVariant { get; set; } = null!;
}
