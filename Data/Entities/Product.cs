namespace Nexus.Data.Entities;

public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int CategoryId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Category Category { get; set; } = null!;

    public ICollection<ProductImage> Images { get; set; } = [];

    public ICollection<ProductOption> Options { get; set; } = [];

    public ICollection<ProductVariant> Variants { get; set; } = [];
}
