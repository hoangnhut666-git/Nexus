namespace Nexus.Services.Products.Models;

public sealed class ProductOptionValueDto
{
    public int Id { get; set; }

    public string Value { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
