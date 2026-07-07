using Microsoft.EntityFrameworkCore;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Services.Categories.Models;
using Nexus.Services.Products.Models;

namespace Nexus.Services.Products;

public sealed class ProductService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IProductImageService imageService) : IProductService
{
    public async Task<PagedResult<ProductListItemDto>> GetPagedAsync(
        ProductQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 6 : query.PageSize;

        var products = context.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            products = products.Where(p =>
                p.Name.Contains(search)
                || p.Slug.Contains(search)
                || p.Variants.Any(v => v.Sku.Contains(search)));
        }

        if (query.CategoryId.HasValue)
            products = products.Where(p => p.CategoryId == query.CategoryId.Value);

        products = query.Status switch
        {
            ProductStatusFilter.Active => products.Where(p => p.IsActive),
            ProductStatusFilter.Hidden => products.Where(p => !p.IsActive),
            _ => products
        };

        var totalCount = await products.CountAsync(cancellationToken);

        var items = await products
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListItemDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                ThumbnailUrl = p.Images
                    .OrderBy(i => i.SortOrder)
                    .ThenBy(i => i.Id)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault(),
                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name,
                Sku = p.Variants
                    .OrderBy(v => v.Id)
                    .Select(v => v.Sku)
                    .FirstOrDefault() ?? string.Empty,
                Price = p.Variants.Any()
                    ? p.Variants.Min(v => v.Price)
                    : 0,
                TotalStock = p.Variants.Sum(v => v.StockQuantity),
                IsActive = p.IsActive
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ServiceResult<ProductDetailDto>> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<ProductDetailDto>.Fail("Product name is required.");

        if (request.Price < 0)
            return ServiceResult<ProductDetailDto>.Fail("Price must be zero or greater.");

        if (request.StockQuantity < 0)
            return ServiceResult<ProductDetailDto>.Fail("Stock quantity must be zero or greater.");

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var categoryExists = await context.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
            return ServiceResult<ProductDetailDto>.Fail("Category not found.");

        var baseSlug = string.IsNullOrWhiteSpace(request.Slug)
            ? GenerateSlug(request.Name)
            : NormalizeSlug(request.Slug);

        if (string.IsNullOrWhiteSpace(baseSlug))
            return ServiceResult<ProductDetailDto>.Fail("Slug is required.");

        var slug = await EnsureUniqueSlugAsync(context, baseSlug, cancellationToken: cancellationToken);

        var sku = string.IsNullOrWhiteSpace(request.Sku)
            ? $"{slug}-default"
            : request.Sku.Trim();

        if (await IsSkuTakenAsync(context, sku, cancellationToken: cancellationToken))
            return ServiceResult<ProductDetailDto>.Fail("A product variant with this SKU already exists.");

        var now = DateTime.UtcNow;
        var product = new Product
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CategoryId = request.CategoryId,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
            Variants =
            [
                new ProductVariant
                {
                    Sku = sku,
                    Price = request.Price,
                    StockQuantity = request.StockQuantity,
                    IsActive = request.VariantIsActive
                }
            ]
        };

        var primaryImageUrl = NormalizeImageUrl(request.PrimaryImageUrl);
        if (primaryImageUrl is not null)
        {
            product.Images.Add(new ProductImage
            {
                ImageUrl = primaryImageUrl,
                SortOrder = 0
            });
        }

        context.Products.Add(product);
        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProductDetailDto>.Ok(
            (await GetByIdAsync(product.Id, cancellationToken))!);
    }

    public async Task<ProductDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var product = await context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Options)
                .ThenInclude(o => o.Values)
            .Include(p => p.Variants)
                .ThenInclude(v => v.OptionValues)
                    .ThenInclude(ov => ov.ProductOptionValue)
                        .ThenInclude(pov => pov.ProductOption)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return product is null ? null : MapToDetailDto(product);
    }

    public async Task<ServiceResult<ProductDetailDto>> UpdateAsync(
        int id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<ProductDetailDto>.Fail("Product name is required.");

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var product = await context.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
            return ServiceResult<ProductDetailDto>.Fail("Product not found.");

        var categoryExists = await context.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
            return ServiceResult<ProductDetailDto>.Fail("Category not found.");

        var slug = NormalizeSlug(request.Slug);
        if (string.IsNullOrWhiteSpace(slug))
            return ServiceResult<ProductDetailDto>.Fail("Slug is required.");

        if (!await IsSlugAvailableAsync(context, slug, id, cancellationToken))
            return ServiceResult<ProductDetailDto>.Fail("A product with this slug already exists.");

        if (request.IsActive && !product.Variants.Any(v => v.IsActive))
            return ServiceResult<ProductDetailDto>.Fail("Product must have at least one active variant before it can be activated.");

        product.Name = request.Name.Trim();
        product.Slug = slug;
        product.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        product.CategoryId = request.CategoryId;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        await UpsertPrimaryImageAsync(context, product, request.PrimaryImageUrl, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProductDetailDto>.Ok(
            (await GetByIdAsync(product.Id, cancellationToken))!);
    }

    public async Task<ServiceResult<bool>> SetActiveAsync(
        int id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var product = await context.Products
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
            return ServiceResult<bool>.Fail("Product not found.");

        if (isActive && !product.Variants.Any(v => v.IsActive))
            return ServiceResult<bool>.Fail("Product must have at least one active variant before it can be activated.");

        product.IsActive = isActive;
        product.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<ProductDetailDto>> SaveOptionsAsync(
        int productId,
        SaveProductOptionsRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var product = await context.Products
            .Include(p => p.Options)
                .ThenInclude(o => o.Values)
            .Include(p => p.Variants)
                .ThenInclude(v => v.OptionValues)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
            return ServiceResult<ProductDetailDto>.Fail("Product not found.");

        var validationError = ValidateOptionInputs(request.Options);
        if (validationError is not null)
            return ServiceResult<ProductDetailDto>.Fail(validationError);

        var requestedValueIds = request.Options
            .SelectMany(o => o.Values)
            .Where(v => v.Id.HasValue)
            .Select(v => v.Id!.Value)
            .ToHashSet();

        var linkedValueIds = product.Variants
            .SelectMany(v => v.OptionValues)
            .Select(ov => ov.ProductOptionValueId)
            .ToHashSet();

        foreach (var linkedId in linkedValueIds)
        {
            if (!requestedValueIds.Contains(linkedId))
            {
                var value = product.Options
                    .SelectMany(o => o.Values)
                    .FirstOrDefault(v => v.Id == linkedId);
                return ServiceResult<ProductDetailDto>.Fail(
                    $"Cannot remove option value \"{value?.Value ?? linkedId.ToString()}\" because it is used by a variant.");
            }
        }

        var requestedOptionIds = request.Options
            .Where(o => o.Id.HasValue)
            .Select(o => o.Id!.Value)
            .ToHashSet();

        var optionsToRemove = product.Options
            .Where(o => !requestedOptionIds.Contains(o.Id))
            .ToList();

        foreach (var option in optionsToRemove)
            context.ProductOptions.Remove(option);

        foreach (var optionInput in request.Options.OrderBy(o => o.SortOrder))
        {
            ProductOption option;
            if (optionInput.Id.HasValue)
            {
                option = product.Options.First(o => o.Id == optionInput.Id.Value);
                option.Name = optionInput.Name.Trim();
                option.SortOrder = optionInput.SortOrder;
            }
            else
            {
                option = new ProductOption
                {
                    ProductId = product.Id,
                    Name = optionInput.Name.Trim(),
                    SortOrder = optionInput.SortOrder
                };
                product.Options.Add(option);
            }

            var requestedIdsForOption = optionInput.Values
                .Where(v => v.Id.HasValue)
                .Select(v => v.Id!.Value)
                .ToHashSet();

            var valuesToRemove = option.Values
                .Where(v => !requestedIdsForOption.Contains(v.Id))
                .ToList();

            foreach (var value in valuesToRemove)
                context.ProductOptionValues.Remove(value);

            foreach (var valueInput in optionInput.Values.OrderBy(v => v.SortOrder))
            {
                if (valueInput.Id.HasValue)
                {
                    var existing = option.Values.First(v => v.Id == valueInput.Id.Value);
                    existing.Value = valueInput.Value.Trim();
                    existing.SortOrder = valueInput.SortOrder;
                }
                else
                {
                    option.Values.Add(new ProductOptionValue
                    {
                        Value = valueInput.Value.Trim(),
                        SortOrder = valueInput.SortOrder
                    });
                }
            }
        }

        product.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProductDetailDto>.Ok(
            (await GetByIdAsync(product.Id, cancellationToken))!);
    }

    public async Task<ServiceResult<IReadOnlyList<ProductVariantDto>>> GenerateVariantsAsync(
        int productId,
        GenerateVariantsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.DefaultPrice < 0)
            return ServiceResult<IReadOnlyList<ProductVariantDto>>.Fail("Price must be zero or greater.");

        if (request.DefaultStock < 0)
            return ServiceResult<IReadOnlyList<ProductVariantDto>>.Fail("Stock quantity must be zero or greater.");

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var product = await context.Products
            .Include(p => p.Options)
                .ThenInclude(o => o.Values)
            .Include(p => p.Variants)
                .ThenInclude(v => v.OptionValues)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
            return ServiceResult<IReadOnlyList<ProductVariantDto>>.Fail("Product not found.");

        var options = product.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Id).ToList();
        if (options.Count == 0 || options.Any(o => o.Values.Count == 0))
            return ServiceResult<IReadOnlyList<ProductVariantDto>>.Fail("Product must have at least one option with values before generating variants.");

        var valueSets = options
            .Select(o => o.Values.OrderBy(v => v.SortOrder).ThenBy(v => v.Id).ToList())
            .ToList();

        var combinations = CartesianProduct(valueSets).ToList();
        var existingSignatures = product.Variants
            .Select(v => GetOptionValueSignature(v.OptionValues.Select(ov => ov.ProductOptionValueId)))
            .ToHashSet(StringComparer.Ordinal);

        var created = new List<ProductVariant>();

        foreach (var combo in combinations)
        {
            var signature = GetOptionValueSignature(combo.Select(v => v.Id));
            if (existingSignatures.Contains(signature))
                continue;

            var valueLabels = combo.Select(v => v.Value).ToList();
            var sku = ProductSkuHelper.GenerateSku(product.Slug, valueLabels, request.SkuPrefix);

            if (await IsSkuTakenAsync(context, sku, cancellationToken: cancellationToken))
                return ServiceResult<IReadOnlyList<ProductVariantDto>>.Fail($"A product variant with SKU \"{sku}\" already exists.");

            var variant = new ProductVariant
            {
                ProductId = product.Id,
                Sku = sku,
                Price = request.DefaultPrice,
                StockQuantity = request.DefaultStock,
                IsActive = request.DefaultIsActive,
                OptionValues = combo.Select(v => new VariantOptionValue
                {
                    ProductOptionValueId = v.Id
                }).ToList()
            };

            product.Variants.Add(variant);
            created.Add(variant);
            existingSignatures.Add(signature);
        }

        if (created.Count > 0)
        {
            product.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        var detail = await GetByIdAsync(product.Id, cancellationToken);
        var createdIds = created.Select(v => v.Id).ToHashSet();
        var dtos = detail!.Variants.Where(v => createdIds.Contains(v.Id)).ToList();

        return ServiceResult<IReadOnlyList<ProductVariantDto>>.Ok(dtos);
    }

    public async Task<ServiceResult<ProductDetailDto>> UpsertVariantsAsync(
        int productId,
        UpsertVariantsRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var product = await context.Products
            .Include(p => p.Options)
                .ThenInclude(o => o.Values)
            .Include(p => p.Variants)
                .ThenInclude(v => v.OptionValues)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
            return ServiceResult<ProductDetailDto>.Fail("Product not found.");

        foreach (var deleteId in request.DeleteVariantIds)
        {
            var variant = product.Variants.FirstOrDefault(v => v.Id == deleteId);
            if (variant is not null)
                context.ProductVariants.Remove(variant);
        }

        foreach (var input in request.Variants)
        {
            if (input.Price < 0)
                return ServiceResult<ProductDetailDto>.Fail("Price must be zero or greater.");

            if (input.StockQuantity < 0)
                return ServiceResult<ProductDetailDto>.Fail("Stock quantity must be zero or greater.");

            if (string.IsNullOrWhiteSpace(input.Sku))
                return ServiceResult<ProductDetailDto>.Fail("SKU is required.");

            var validationError = ValidateVariantOptionValues(product, input.OptionValueIds);
            if (validationError is not null)
                return ServiceResult<ProductDetailDto>.Fail(validationError);

            if (await IsSkuTakenAsync(context, input.Sku.Trim(), input.Id, cancellationToken))
                return ServiceResult<ProductDetailDto>.Fail("A product variant with this SKU already exists.");

            if (input.Id.HasValue)
            {
                var variant = product.Variants.FirstOrDefault(v => v.Id == input.Id.Value);
                if (variant is null)
                    return ServiceResult<ProductDetailDto>.Fail($"Variant {input.Id.Value} not found.");

                variant.Sku = input.Sku.Trim();
                variant.Price = input.Price;
                variant.StockQuantity = input.StockQuantity;
                variant.IsActive = input.IsActive;
                variant.ImageUrl = NormalizeImageUrl(input.ImageUrl);

                context.VariantOptionValues.RemoveRange(variant.OptionValues);
                variant.OptionValues = input.OptionValueIds
                    .Select(id => new VariantOptionValue { ProductOptionValueId = id })
                    .ToList();
            }
            else
            {
                product.Variants.Add(new ProductVariant
                {
                    ProductId = product.Id,
                    Sku = input.Sku.Trim(),
                    Price = input.Price,
                    StockQuantity = input.StockQuantity,
                    IsActive = input.IsActive,
                    ImageUrl = NormalizeImageUrl(input.ImageUrl),
                    OptionValues = input.OptionValueIds
                        .Select(id => new VariantOptionValue { ProductOptionValueId = id })
                        .ToList()
                });
            }
        }

        if (product.IsActive && !product.Variants.Any(v => v.IsActive))
            product.IsActive = false;

        product.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<ProductDetailDto>.Ok(
            (await GetByIdAsync(product.Id, cancellationToken))!);
    }

    public async Task<ServiceResult<int>> BulkUpdateVariantsAsync(
        int productId,
        BulkVariantActionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.VariantIds.Count == 0)
            return ServiceResult<int>.Fail("No variants selected.");

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var variants = await context.ProductVariants
            .Where(v => v.ProductId == productId && request.VariantIds.Contains(v.Id))
            .ToListAsync(cancellationToken);

        if (variants.Count == 0)
            return ServiceResult<int>.Fail("No matching variants found.");

        switch (request.Action)
        {
            case BulkVariantAction.SetPrice:
                if (!request.Price.HasValue || request.Price.Value < 0)
                    return ServiceResult<int>.Fail("A valid price is required.");
                foreach (var variant in variants)
                    variant.Price = request.Price.Value;
                break;

            case BulkVariantAction.AdjustStock:
                if (!request.StockDelta.HasValue)
                    return ServiceResult<int>.Fail("Stock delta is required.");
                foreach (var variant in variants)
                    variant.StockQuantity = Math.Max(0, variant.StockQuantity + request.StockDelta.Value);
                break;

            case BulkVariantAction.SetActive:
                if (!request.IsActive.HasValue)
                    return ServiceResult<int>.Fail("Active status is required.");
                foreach (var variant in variants)
                    variant.IsActive = request.IsActive.Value;
                break;

            default:
                return ServiceResult<int>.Fail("Unknown bulk action.");
        }

        var product = await context.Products
            .Include(p => p.Variants)
            .FirstAsync(p => p.Id == productId, cancellationToken);

        if (product.IsActive && !product.Variants.Any(v => v.IsActive))
            product.IsActive = false;

        product.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<int>.Ok(variants.Count);
    }

    public string GenerateSlug(string name) => ProductSlugHelper.GenerateSlug(name);

    public async Task<bool> IsSlugAvailableAsync(
        string slug,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await IsSlugAvailableAsync(context, slug, excludeId, cancellationToken);
    }

    private static async Task<bool> IsSlugAvailableAsync(
        ApplicationDbContext context,
        string slug,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = NormalizeSlug(slug);
        if (string.IsNullOrWhiteSpace(normalizedSlug))
            return false;

        var query = context.Products.AsNoTracking().Where(p => p.Slug == normalizedSlug);
        if (excludeId.HasValue)
            query = query.Where(p => p.Id != excludeId.Value);

        return !await query.AnyAsync(cancellationToken);
    }

    private static ProductDetailDto MapToDetailDto(Product product)
    {
        var options = product.Options
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.Id)
            .Select(o => new ProductOptionDto
            {
                Id = o.Id,
                Name = o.Name,
                SortOrder = o.SortOrder,
                Values = o.Values
                    .OrderBy(v => v.SortOrder)
                    .ThenBy(v => v.Id)
                    .Select(v => new ProductOptionValueDto
                    {
                        Id = v.Id,
                        Value = v.Value,
                        SortOrder = v.SortOrder
                    })
                    .ToList()
            })
            .ToList();

        return new ProductDetailDto
        {
            Id = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            Description = product.Description,
            CategoryId = product.CategoryId,
            CategoryName = product.Category.Name,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            Options = options,
            Variants = product.Variants
                .OrderBy(v => v.Id)
                .Select(v => MapVariantDto(v, options))
                .ToList(),
            Images = product.Images
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.Id)
                .Select(i => new ProductImageDto
                {
                    Id = i.Id,
                    ImageUrl = i.ImageUrl,
                    SortOrder = i.SortOrder,
                    AltText = i.AltText
                })
                .ToList()
        };
    }

    private static ProductVariantDto MapVariantDto(ProductVariant variant, IReadOnlyList<ProductOptionDto> options)
    {
        var valueIds = variant.OptionValues
            .Select(ov => ov.ProductOptionValueId)
            .OrderBy(id => id)
            .ToList();

        var labels = new List<string>();
        foreach (var option in options.OrderBy(o => o.SortOrder))
        {
            var match = option.Values.FirstOrDefault(v => valueIds.Contains(v.Id));
            if (match is not null)
                labels.Add(match.Value);
        }

        return new ProductVariantDto
        {
            Id = variant.Id,
            Sku = variant.Sku,
            Price = variant.Price,
            StockQuantity = variant.StockQuantity,
            ImageUrl = variant.ImageUrl,
            IsActive = variant.IsActive,
            OptionValueIds = valueIds,
            OptionLabel = string.Join(" / ", labels)
        };
    }

    private static string? ValidateOptionInputs(IReadOnlyList<ProductOptionInput> options)
    {
        if (options.Select(o => o.Name.Trim().ToLowerInvariant()).Distinct().Count() != options.Count)
            return "Duplicate option names are not allowed.";

        foreach (var option in options)
        {
            if (string.IsNullOrWhiteSpace(option.Name))
                return "Option name is required.";

            if (option.Values.Count == 0)
                return $"Option \"{option.Name}\" must have at least one value.";

            if (option.Values.Any(v => string.IsNullOrWhiteSpace(v.Value)))
                return $"All values for option \"{option.Name}\" must be non-empty.";
        }

        return null;
    }

    private static string? ValidateVariantOptionValues(Product product, IReadOnlyList<int> optionValueIds)
    {
        var optionCount = product.Options.Count;
        if (optionCount == 0)
        {
            if (optionValueIds.Count > 0)
                return "Cannot assign option values to a product without options.";
            return null;
        }

        if (optionValueIds.Count != optionCount)
            return "Each variant must have exactly one value per product option.";

        var allValues = product.Options.SelectMany(o => o.Values).ToList();
        var selected = new List<ProductOptionValue>();

        foreach (var id in optionValueIds)
        {
            var value = allValues.FirstOrDefault(v => v.Id == id);
            if (value is null)
                return "One or more option values are invalid for this product.";
            selected.Add(value);
        }

        if (selected.Select(v => v.ProductOptionId).Distinct().Count() != optionCount)
            return "Each variant must have exactly one value per product option.";

        return null;
    }

    private static IEnumerable<List<ProductOptionValue>> CartesianProduct(IReadOnlyList<List<ProductOptionValue>> sets)
    {
        if (sets.Count == 0)
        {
            yield return [];
            yield break;
        }

        foreach (var head in sets[0])
        {
            if (sets.Count == 1)
            {
                yield return [head];
                continue;
            }

            foreach (var tail in CartesianProduct(sets.Skip(1).ToList()))
            {
                var combo = new List<ProductOptionValue> { head };
                combo.AddRange(tail);
                yield return combo;
            }
        }
    }

    private static string GetOptionValueSignature(IEnumerable<int> optionValueIds) =>
        string.Join(",", optionValueIds.OrderBy(id => id));

    private async Task UpsertPrimaryImageAsync(
        ApplicationDbContext context,
        Product product,
        string? primaryImageUrl,
        CancellationToken cancellationToken)
    {
        var newImageUrl = NormalizeImageUrl(primaryImageUrl);
        var primaryImage = product.Images
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .FirstOrDefault();

        if (primaryImage is null)
        {
            if (newImageUrl is not null)
            {
                product.Images.Add(new ProductImage
                {
                    ImageUrl = newImageUrl,
                    SortOrder = 0
                });
            }

            return;
        }

        if (string.Equals(primaryImage.ImageUrl, newImageUrl, StringComparison.OrdinalIgnoreCase))
            return;

        if (imageService.IsLocalUploadPath(primaryImage.ImageUrl))
            await imageService.DeleteFileIfLocalAsync(primaryImage.ImageUrl);

        if (newImageUrl is null)
        {
            context.ProductImages.Remove(primaryImage);
            return;
        }

        primaryImage.ImageUrl = newImageUrl;
    }

    private static async Task<string> EnsureUniqueSlugAsync(
        ApplicationDbContext context,
        string baseSlug,
        int? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var candidate = baseSlug;
        var suffix = 2;

        while (!await IsSlugAvailableAsync(context, candidate, excludeId, cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static async Task<bool> IsSkuTakenAsync(
        ApplicationDbContext context,
        string sku,
        int? excludeVariantId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSku = sku.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSku))
            return false;

        var query = context.ProductVariants.AsNoTracking().Where(v => v.Sku == normalizedSku);
        if (excludeVariantId.HasValue)
            query = query.Where(v => v.Id != excludeVariantId.Value);

        return await query.AnyAsync(cancellationToken);
    }

    private static string NormalizeSlug(string slug) =>
        ProductSlugHelper.GenerateSlug(slug);

    private static string? NormalizeImageUrl(string? imageUrl) =>
        string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
}
