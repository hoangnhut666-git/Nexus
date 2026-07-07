namespace Nexus.Services.Products.Models;

public sealed class CatalogProductQuery
{
    public string? Search { get; set; }

    public int? CategoryId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 12;
}
