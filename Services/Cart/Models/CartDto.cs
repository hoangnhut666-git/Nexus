namespace Nexus.Services.Cart.Models;

public sealed class CartDto
{
    public IReadOnlyList<CartLineDto> Items { get; init; } = [];

    public int TotalQuantity { get; init; }

    public decimal Subtotal { get; init; }

    public decimal ShippingFee { get; init; }

    public decimal TaxAmount { get; init; }

    public decimal Total { get; init; }

    public bool IsEmpty => Items.Count == 0;
}
