using System.ComponentModel.DataAnnotations;

namespace Nexus.Services.Categories.Models;

public sealed class UpdateCategoryRequest
{
    [Required(ErrorMessage = "Category name is required.")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Slug is required.")]
    [MaxLength(200)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;
}
