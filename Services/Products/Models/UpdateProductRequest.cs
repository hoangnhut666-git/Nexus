using System.ComponentModel.DataAnnotations;

namespace Nexus.Services.Products.Models;

public sealed class UpdateProductRequest
{
    [Required(ErrorMessage = "Product name is required.")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Slug is required.")]
    [MaxLength(200)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    public int CategoryId { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? PrimaryImageUrl { get; set; }
}
