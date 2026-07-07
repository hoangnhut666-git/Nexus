using System.ComponentModel.DataAnnotations;

namespace Nexus.Services.Cart.Models;

public sealed class UpdateCartItemRequest
{
    [Range(0, int.MaxValue, ErrorMessage = "Quantity must be zero or greater.")]
    public int Quantity { get; set; }
}
