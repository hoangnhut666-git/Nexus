using System.ComponentModel.DataAnnotations;

namespace Nexus.Services.Products.Models;

public sealed class CreateProductRequest
{
    [Required(ErrorMessage = "Product name is required.")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Slug { get; set; }

    [MaxLength(4000)]
    public string? Description { get; set; }

    public int CategoryId { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(50)]
    public string? Sku { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Price must be zero or greater.")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock quantity must be zero or greater.")]
    public int StockQuantity { get; set; }

    public bool VariantIsActive { get; set; } = true;

    [MaxLength(500)]
    public string? PrimaryImageUrl { get; set; }
}
