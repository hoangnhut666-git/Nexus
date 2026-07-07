namespace Nexus.Services.Orders.Models;

public sealed class OrderLineDto
{
    public int ProductVariantId { get; init; }

    public int ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string VariantLabel { get; init; } = string.Empty;

    public string Sku { get; init; } = string.Empty;

    public decimal UnitPrice { get; init; }

    public int Quantity { get; init; }

    public decimal LineTotal { get; init; }
}
