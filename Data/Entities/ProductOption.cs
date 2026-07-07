namespace Nexus.Data.Entities;

public class ProductOption
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public Product Product { get; set; } = null!;

    public ICollection<ProductOptionValue> Values { get; set; } = [];
}
