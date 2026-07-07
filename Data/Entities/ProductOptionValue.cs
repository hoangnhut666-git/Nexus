namespace Nexus.Data.Entities;

public class ProductOptionValue
{
    public int Id { get; set; }

    public int ProductOptionId { get; set; }

    public string Value { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public ProductOption ProductOption { get; set; } = null!;

    public ICollection<VariantOptionValue> VariantLinks { get; set; } = [];
}
