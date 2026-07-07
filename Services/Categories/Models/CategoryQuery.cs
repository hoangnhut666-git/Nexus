namespace Nexus.Services.Categories.Models;

public enum CategoryStatusFilter
{
    All,
    Active,
    Hidden
}

public sealed class CategoryQuery
{
    public string? Search { get; set; }

    public CategoryStatusFilter Status { get; set; } = CategoryStatusFilter.All;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 6;
}
