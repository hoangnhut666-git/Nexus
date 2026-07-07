namespace Nexus.Services.Products.Models;

public sealed class CatalogProductListItemDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string? ThumbnailUrl { get; init; }

    public int CategoryId { get; init; }

    public string CategoryName { get; init; } = string.Empty;

    public decimal MinPrice { get; init; }

    public int TotalStock { get; init; }

    public bool InStock { get; init; }

    public bool HasOptions { get; init; }
}
