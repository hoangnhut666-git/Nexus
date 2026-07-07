namespace Nexus.Services.Products.Models;

public sealed class ProductDetailDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public IReadOnlyList<ProductVariantDto> Variants { get; set; } = [];

    public IReadOnlyList<ProductImageDto> Images { get; set; } = [];

    public IReadOnlyList<ProductOptionDto> Options { get; set; } = [];
}
