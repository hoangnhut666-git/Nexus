namespace Nexus.Data.Entities;

public class ProductImage
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public string? AltText { get; set; }

    public Product Product { get; set; } = null!;
}
