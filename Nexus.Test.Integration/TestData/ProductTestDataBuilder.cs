using Bogus;
using Nexus.Services.Products.Models;

namespace Nexus.Test.Integration.TestData;

public static class ProductTestDataBuilder
{
    public static CreateProductRequest ValidCreateProductRequest(int categoryId) => new()
    {
        Name = "CyberType S87 Keyboard",
        Description = "Wireless mechanical keyboard",
        CategoryId = categoryId,
        IsActive = true,
        Price = 2_450_000m,
        StockQuantity = 124,
        VariantIsActive = true
    };

    public static CreateProductRequest ValidCreateProductRequestWithSku(int categoryId, string sku) => new()
    {
        Name = "Nexus Sound Pro X",
        Slug = "nexus-sound-pro-x",
        CategoryId = categoryId,
        IsActive = true,
        Sku = sku,
        Price = 3_200_000m,
        StockQuantity = 50,
        VariantIsActive = true
    };

    public static Product ValidProductWithSlug(int categoryId, string name, string slug) => new()
    {
        Name = name,
        Slug = slug,
        Description = "Test product",
        CategoryId = categoryId,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static Faker<CreateProductRequest> CreateProductRequestFaker(int categoryId) => new Faker<CreateProductRequest>()
        .RuleFor(r => r.Name, f => f.Commerce.ProductName())
        .RuleFor(r => r.Description, f => f.Commerce.ProductDescription())
        .RuleFor(r => r.CategoryId, _ => categoryId)
        .RuleFor(r => r.IsActive, f => f.Random.Bool(0.9f))
        .RuleFor(r => r.Price, f => f.Random.Decimal(100_000, 10_000_000))
        .RuleFor(r => r.StockQuantity, f => f.Random.Int(0, 500))
        .RuleFor(r => r.VariantIsActive, _ => true);
}
