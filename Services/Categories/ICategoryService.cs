using Nexus.Services.Categories.Models;

namespace Nexus.Services.Categories;

public interface ICategoryService
{
    Task<PagedResult<CategoryListItemDto>> GetPagedAsync(CategoryQuery query, CancellationToken cancellationToken = default);

    Task<CategoryDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult<CategoryDetailDto>> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<CategoryDetailDto>> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> IsSlugAvailableAsync(string slug, int? excludeId = null, CancellationToken cancellationToken = default);

    string GenerateSlug(string name);
}
