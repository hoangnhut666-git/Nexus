namespace Nexus.Services.Cart.Models;

public sealed class CartLineDto
{
    public int CartItemId { get; init; }

    public int ProductVariantId { get; init; }

    public int ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string VariantLabel { get; init; } = string.Empty;

    public string Sku { get; init; } = string.Empty;

    public string? ImageUrl { get; init; }

    public decimal UnitPrice { get; init; }

    public int Quantity { get; init; }

    public int StockQuantity { get; init; }

    public bool InStock => StockQuantity > 0;

    public decimal LineTotal => UnitPrice * Quantity;
}
