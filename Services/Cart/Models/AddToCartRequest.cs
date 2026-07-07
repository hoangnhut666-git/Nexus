using System.ComponentModel.DataAnnotations;

namespace Nexus.Services.Cart.Models;

public sealed class AddToCartRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "A valid product variant is required.")]
    public int ProductVariantId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least one.")]
    public int Quantity { get; set; } = 1;

    public AddToCartRequest()
    {
    }

    public AddToCartRequest(int productVariantId, int quantity = 1)
    {
        ProductVariantId = productVariantId;
        Quantity = quantity;
    }
}
