namespace Nexus.Services.Products.Models;

public enum ProductStatusFilter
{
    All,
    Active,
    Hidden
}

public sealed class ProductQuery
{
    public string? Search { get; set; }

    public int? CategoryId { get; set; }

    public ProductStatusFilter Status { get; set; } = ProductStatusFilter.Active;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 6;
}
