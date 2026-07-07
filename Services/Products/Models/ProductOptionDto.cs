namespace Nexus.Services.Products.Models;

public sealed class ProductOptionDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public IReadOnlyList<ProductOptionValueDto> Values { get; set; } = [];
}
