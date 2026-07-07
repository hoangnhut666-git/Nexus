using Microsoft.EntityFrameworkCore;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Services.Categories.Models;

namespace Nexus.Services.Categories;

public sealed class CategoryService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ICategoryImageService imageService) : ICategoryService
{
    public async Task<PagedResult<CategoryListItemDto>> GetPagedAsync(
        CategoryQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 6 : query.PageSize;

        var categories = context.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            categories = categories.Where(c =>
                c.Name.Contains(search) || c.Slug.Contains(search));
        }

        categories = query.Status switch
        {
            CategoryStatusFilter.Active => categories.Where(c => c.IsActive),
            CategoryStatusFilter.Hidden => categories.Where(c => !c.IsActive),
            _ => categories
        };

        var totalCount = await categories.CountAsync(cancellationToken);

        var items = await categories
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CategoryListItemDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                ImageUrl = c.ImageUrl,
                IsActive = c.IsActive,
                ProductCount = c.Products.Count
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<CategoryListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CategoryDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDetailDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                ImageUrl = c.ImageUrl,
                IsActive = c.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ServiceResult<CategoryDetailDto>> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var slug = NormalizeSlug(request.Slug);
        if (string.IsNullOrWhiteSpace(slug))
            return ServiceResult<CategoryDetailDto>.Fail("Slug is required.");

        if (!await IsSlugAvailableAsync(context, slug, cancellationToken: cancellationToken))
            return ServiceResult<CategoryDetailDto>.Fail("A category with this slug already exists.");

        var now = DateTime.UtcNow;
        var category = new Category
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ImageUrl = NormalizeImageUrl(request.ImageUrl),
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Categories.Add(category);
        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<CategoryDetailDto>.Ok(MapToDetail(category));
    }

    public async Task<ServiceResult<CategoryDetailDto>> UpdateAsync(
        int id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var category = await context.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
            return ServiceResult<CategoryDetailDto>.Fail("Category not found.");

        var slug = NormalizeSlug(request.Slug);
        if (string.IsNullOrWhiteSpace(slug))
            return ServiceResult<CategoryDetailDto>.Fail("Slug is required.");

        if (!await IsSlugAvailableAsync(context, slug, id, cancellationToken))
            return ServiceResult<CategoryDetailDto>.Fail("A category with this slug already exists.");

        var newImageUrl = NormalizeImageUrl(request.ImageUrl);
        if (!string.Equals(category.ImageUrl, newImageUrl, StringComparison.OrdinalIgnoreCase)
            && imageService.IsLocalUploadPath(category.ImageUrl))
        {
            await imageService.DeleteFileIfLocalAsync(category.ImageUrl);
        }

        category.Name = request.Name.Trim();
        category.Slug = slug;
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        category.ImageUrl = newImageUrl;
        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<CategoryDetailDto>.Ok(MapToDetail(category));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var category = await context.Categories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category is null)
            return ServiceResult<bool>.Fail("Category not found.");

        if (category.Products.Count > 0)
            return ServiceResult<bool>.Fail("Category contains products and cannot be deleted.");

        await imageService.DeleteFileIfLocalAsync(category.ImageUrl);
        context.Categories.Remove(category);
        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<bool> IsSlugAvailableAsync(
        string slug,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await IsSlugAvailableAsync(context, slug, excludeId, cancellationToken);
    }

    public string GenerateSlug(string name) => CategorySlugHelper.GenerateSlug(name);

    private static async Task<bool> IsSlugAvailableAsync(
        ApplicationDbContext context,
        string slug,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = NormalizeSlug(slug);
        if (string.IsNullOrWhiteSpace(normalizedSlug))
            return false;

        var query = context.Categories.AsNoTracking().Where(c => c.Slug == normalizedSlug);
        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);

        return !await query.AnyAsync(cancellationToken);
    }

    private static CategoryDetailDto MapToDetail(Category category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Slug = category.Slug,
        Description = category.Description,
        ImageUrl = category.ImageUrl,
        IsActive = category.IsActive
    };

    private static string NormalizeSlug(string slug) =>
        CategorySlugHelper.GenerateSlug(slug);

    private static string? NormalizeImageUrl(string? imageUrl) =>
        string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
}
