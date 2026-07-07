namespace Nexus.Services.Categories.Models;

public sealed class CategoryDetailDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? ImageUrl { get; init; }

    public bool IsActive { get; init; }
}
