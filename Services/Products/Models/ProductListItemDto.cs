namespace Nexus.Services.Products.Models;

public sealed class ProductListItemDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string? ThumbnailUrl { get; init; }

    public int CategoryId { get; init; }

    public string CategoryName { get; init; } = string.Empty;

    public string Sku { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public int TotalStock { get; init; }

    public bool IsActive { get; init; }
}
