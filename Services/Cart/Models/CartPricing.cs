namespace Nexus.Services.Cart.Models;

/// <summary>
/// Centralizes the cart summary math in USD. Shipping and tax use simple fixed rules;
/// promo/discount codes are intentionally out of scope until the checkout roadmap.
/// </summary>
public static class CartPricing
{
    public const decimal TaxRate = 0.08m;

    public const decimal StandardShippingFee = 9.99m;

    public const decimal FreeShippingThreshold = 99m;

    public static CartDto BuildCart(IReadOnlyList<CartLineDto> items)
    {
        var subtotal = items.Sum(i => i.LineTotal);
        var totalQuantity = items.Sum(i => i.Quantity);

        var shipping = items.Count == 0 || subtotal >= FreeShippingThreshold
            ? 0m
            : StandardShippingFee;

        var tax = Math.Round(subtotal * TaxRate, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + shipping + tax;

        return new CartDto
        {
            Items = items,
            TotalQuantity = totalQuantity,
            Subtotal = subtotal,
            ShippingFee = shipping,
            TaxAmount = tax,
            Total = total
        };
    }
}
