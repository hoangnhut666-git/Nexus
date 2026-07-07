namespace Nexus.Services.Products.Models;

public sealed class ProductImageDto
{
    public int Id { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public string? AltText { get; set; }
}
