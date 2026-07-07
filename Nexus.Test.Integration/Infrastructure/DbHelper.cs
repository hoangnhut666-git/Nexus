namespace Nexus.Test.Integration.Infrastructure;

/// <summary>
/// Provides a standalone <see cref="ApplicationDbContext"/> for direct database assertions.
/// Using a separate context ensures you read persisted state, not a scoped factory cache.
/// </summary>
public sealed class DbHelper(string connectionString)
{
    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    public async Task<bool> UserExistsAsync(string userId)
    {
        await using var db = CreateContext();
        return await db.Users.AnyAsync(u => u.Id == userId);
    }

    public async Task<ApplicationUser?> GetUserAsync(string email)
    {
        await using var db = CreateContext();
        return await db.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<bool> RoleExistsAsync(string roleName)
    {
        await using var db = CreateContext();
        return await db.Roles.AnyAsync(r => r.Name == roleName);
    }

    public async Task<Category?> GetCategoryAsync(int id)
    {
        await using var db = CreateContext();
        return await db.Categories.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Category?> GetCategoryBySlugAsync(string slug)
    {
        await using var db = CreateContext();
        return await db.Categories.FirstOrDefaultAsync(c => c.Slug == slug);
    }

    public async Task<int> GetCategoryProductCountAsync(int categoryId)
    {
        await using var db = CreateContext();
        return await db.Products.CountAsync(p => p.CategoryId == categoryId);
    }

    public async Task<Category> InsertCategoryAsync(Category category)
    {
        await using var db = CreateContext();
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    public async Task<Product> InsertProductAsync(Product product)
    {
        await using var db = CreateContext();
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    public async Task<Product?> GetProductBySlugAsync(string slug)
    {
        await using var db = CreateContext();
        return await db.Products.FirstOrDefaultAsync(p => p.Slug == slug);
    }

    public async Task<IReadOnlyList<ProductVariant>> GetProductVariantsAsync(int productId)
    {
        await using var db = CreateContext();
        return await db.ProductVariants
            .Where(v => v.ProductId == productId)
            .OrderBy(v => v.Id)
            .ToListAsync();
    }

    public async Task<ProductVariant?> GetVariantBySkuAsync(string sku)
    {
        await using var db = CreateContext();
        return await db.ProductVariants.FirstOrDefaultAsync(v => v.Sku == sku);
    }

    public async Task<ProductVariant> InsertProductVariantAsync(
        int productId,
        string sku,
        decimal price,
        int stockQuantity)
    {
        await using var db = CreateContext();
        var variant = new ProductVariant
        {
            ProductId = productId,
            Sku = sku,
            Price = price,
            StockQuantity = stockQuantity,
            IsActive = true
        };
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();
        return variant;
    }

    public async Task<ProductOption> InsertProductOptionAsync(int productId, string name, int sortOrder = 0)
    {
        await using var db = CreateContext();
        var option = new ProductOption
        {
            ProductId = productId,
            Name = name,
            SortOrder = sortOrder
        };
        db.ProductOptions.Add(option);
        await db.SaveChangesAsync();
        return option;
    }

    public async Task<ProductOptionValue> InsertProductOptionValueAsync(int optionId, string value, int sortOrder = 0)
    {
        await using var db = CreateContext();
        var optionValue = new ProductOptionValue
        {
            ProductOptionId = optionId,
            Value = value,
            SortOrder = sortOrder
        };
        db.ProductOptionValues.Add(optionValue);
        await db.SaveChangesAsync();
        return optionValue;
    }

    public async Task<ProductVariant?> GetVariantByOptionValueIdsAsync(int productId, IReadOnlyList<int> optionValueIds)
    {
        await using var db = CreateContext();
        var sortedIds = optionValueIds.OrderBy(id => id).ToList();

        return await db.ProductVariants
            .Include(v => v.OptionValues)
            .Where(v => v.ProductId == productId)
            .FirstOrDefaultAsync(v =>
                v.OptionValues.Select(ov => ov.ProductOptionValueId).OrderBy(id => id).SequenceEqual(sortedIds));
    }
}
