namespace Nexus.Services.Products.Models;

public sealed class ProductOptionValueInput
{
    public int? Id { get; set; }

    public string Value { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}

public sealed class ProductOptionInput
{
    public int? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public IReadOnlyList<ProductOptionValueInput> Values { get; set; } = [];
}

public sealed class SaveProductOptionsRequest
{
    public IReadOnlyList<ProductOptionInput> Options { get; set; } = [];
}
