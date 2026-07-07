using Nexus.Services.Categories.Models;
using Nexus.Services.Products.Models;

namespace Nexus.Services.Products;

public interface IProductService
{
    Task<PagedResult<ProductListItemDto>> GetPagedAsync(
        ProductQuery query,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductDetailDto>> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductDetailDto>> UpdateAsync(
        int id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> SetActiveAsync(
        int id,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductDetailDto>> SaveOptionsAsync(
        int productId,
        SaveProductOptionsRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<ProductVariantDto>>> GenerateVariantsAsync(
        int productId,
        GenerateVariantsRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ProductDetailDto>> UpsertVariantsAsync(
        int productId,
        UpsertVariantsRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<int>> BulkUpdateVariantsAsync(
        int productId,
        BulkVariantActionRequest request,
        CancellationToken cancellationToken = default);

    string GenerateSlug(string name);

    Task<bool> IsSlugAvailableAsync(
        string slug,
        int? excludeId = null,
        CancellationToken cancellationToken = default);
}
