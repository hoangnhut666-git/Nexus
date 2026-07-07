using Microsoft.Extensions.DependencyInjection;
using Nexus.Services.Categories.Models;
using Nexus.Services.Products;
using Nexus.Services.Products.Models;
using Nexus.Test.Integration.TestData;

namespace Nexus.Test.Integration.Features.Product;

public sealed class ProductCatalogServiceTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;
    private readonly DbHelper _dbHelper;

    public ProductCatalogServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _factory = new TestWebApplicationFactory(fixture.ConnectionString);
        _dbHelper = new DbHelper(fixture.ConnectionString);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ResetAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetCatalogPagedAsync_IncludesActiveProductWithActiveVariant()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Catalog Cat", "catalog-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        await service.CreateAsync(new CreateProductRequest
        {
            Name = "Catalog Keyboard",
            Slug = "catalog-keyboard",
            CategoryId = category.Id,
            Price = 1_500_000m,
            StockQuantity = 10,
            IsActive = true,
            VariantIsActive = true
        });

        var result = await service.GetCatalogPagedAsync(new CatalogProductQuery { PageSize = 20 });

        result.Items.Should().Contain(p => p.Slug == "catalog-keyboard");
    }

    [Fact]
    public async Task GetCatalogPagedAsync_ExcludesInactiveProduct()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Hidden Cat", "hidden-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        await service.CreateAsync(new CreateProductRequest
        {
            Name = "Hidden Product",
            Slug = "hidden-product",
            CategoryId = category.Id,
            Price = 100_000m,
            StockQuantity = 5,
            IsActive = false,
            VariantIsActive = true
        });

        var result = await service.GetCatalogPagedAsync(new CatalogProductQuery { PageSize = 50 });

        result.Items.Should().NotContain(p => p.Slug == "hidden-product");
    }

    [Fact]
    public async Task GetCatalogPagedAsync_ExcludesProductWithNoActiveVariants()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("No Active Var", "no-active-var"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var created = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Deactivated Variants",
            Slug = "deactivated-variants",
            CategoryId = category.Id,
            Price = 200_000m,
            StockQuantity = 5,
            IsActive = true,
            VariantIsActive = true
        });

        var variant = created.Data!.Variants[0];
        await service.UpsertVariantsAsync(created.Data.Id, new UpsertVariantsRequest
        {
            Variants =
            [
                new VariantUpsertInput
                {
                    Id = variant.Id,
                    Sku = variant.Sku,
                    Price = variant.Price,
                    StockQuantity = variant.StockQuantity,
                    IsActive = false,
                    OptionValueIds = []
                }
            ]
        });

        var result = await service.GetCatalogPagedAsync(new CatalogProductQuery { PageSize = 50 });

        result.Items.Should().NotContain(p => p.Slug == "deactivated-variants");
    }

    [Fact]
    public async Task GetCatalogPagedAsync_AggregatesOnlyActiveVariants()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Agg Cat", "agg-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var product = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Agg Product",
            Slug = "agg-product",
            CategoryId = category.Id,
            Price = 500_000m,
            StockQuantity = 10,
            IsActive = true,
            VariantIsActive = true
        });

        var saved = await service.SaveOptionsAsync(product.Data!.Id, new SaveProductOptionsRequest
        {
            Options =
            [
                new ProductOptionInput
                {
                    Name = "Size",
                    SortOrder = 0,
                    Values =
                    [
                        new ProductOptionValueInput { Value = "S", SortOrder = 0 },
                        new ProductOptionValueInput { Value = "L", SortOrder = 1 }
                    ]
                }
            ]
        });

        var sizeOption = saved.Data!.Options[0];
        var smallValue = sizeOption.Values.First(v => v.Value == "S");
        var largeValue = sizeOption.Values.First(v => v.Value == "L");
        var defaultVariant = product.Data!.Variants[0];

        await service.UpsertVariantsAsync(product.Data.Id, new UpsertVariantsRequest
        {
            DeleteVariantIds = [defaultVariant.Id],
            Variants =
            [
                new VariantUpsertInput
                {
                    Sku = "agg-product-s",
                    Price = 500_000m,
                    StockQuantity = 10,
                    IsActive = false,
                    OptionValueIds = [smallValue.Id]
                },
                new VariantUpsertInput
                {
                    Sku = "agg-product-l",
                    Price = 800_000m,
                    StockQuantity = 20,
                    IsActive = true,
                    OptionValueIds = [largeValue.Id]
                }
            ]
        });

        var result = await service.GetCatalogPagedAsync(new CatalogProductQuery
        {
            Search = "agg-product",
            PageSize = 10
        });

        var item = result.Items.Should().ContainSingle(p => p.Slug == "agg-product").Subject;
        item.MinPrice.Should().Be(800_000m);
        item.TotalStock.Should().Be(20);
        item.InStock.Should().BeTrue();
    }

    [Fact]
    public async Task GetCatalogPagedAsync_IncludesOutOfStockProductWithInStockFalse()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("OOS Cat", "oos-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        await service.CreateAsync(new CreateProductRequest
        {
            Name = "Out Of Stock Item",
            Slug = "out-of-stock-item",
            CategoryId = category.Id,
            Price = 99_000m,
            StockQuantity = 0,
            IsActive = true,
            VariantIsActive = true
        });

        var result = await service.GetCatalogPagedAsync(new CatalogProductQuery
        {
            Search = "out-of-stock-item",
            PageSize = 10
        });

        var item = result.Items.Should().ContainSingle(p => p.Slug == "out-of-stock-item").Subject;
        item.InStock.Should().BeFalse();
        item.TotalStock.Should().Be(0);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsCatalogFilteredDetail()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Slug Cat", "slug-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var created = await SeedIphoneCatalogProductAsync(service, category.Id, "iphone-catalog");

        var detail = await service.GetBySlugAsync("iphone-catalog");

        detail.Should().NotBeNull();
        detail!.Name.Should().Be("iPhone 16");
        detail.Variants.Should().OnlyContain(v => v.IsActive);
        detail.Options.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNullForInactiveProduct()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Inactive Slug", "inactive-slug"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        await service.CreateAsync(new CreateProductRequest
        {
            Name = "Inactive Slug Product",
            Slug = "inactive-slug-product",
            CategoryId = category.Id,
            Price = 100_000m,
            StockQuantity = 1,
            IsActive = false,
            VariantIsActive = true
        });

        var detail = await service.GetBySlugAsync("inactive-slug-product");

        detail.Should().BeNull();
    }

    [Fact]
    public async Task ResolveVariantAsync_ResolvesYellowAnd32Gb()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Resolve Cat", "resolve-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var created = await SeedIphoneCatalogProductAsync(service, category.Id, "iphone-resolve");

        var detail = await service.GetBySlugAsync("iphone-resolve");
        var yellow = detail!.Options[0].Values.First(v => v.Value == "Yellow");
        var storage32 = detail.Options[1].Values.First(v => v.Value == "32GB");

        var resolved = await service.ResolveVariantAsync(created.Data!.Id, [yellow.Id, storage32.Id]);

        resolved.Should().NotBeNull();
        resolved!.OptionLabel.Should().Be("Yellow / 32GB");
        resolved.Price.Should().Be(24_990_000m);
        resolved.StockQuantity.Should().Be(3);
    }

    [Fact]
    public async Task ResolveVariantAsync_ReturnsNullForInvalidOptionCount()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Invalid Count", "invalid-count"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var created = await SeedIphoneCatalogProductAsync(service, category.Id, "iphone-invalid");

        var detail = await service.GetBySlugAsync("iphone-invalid");
        var yellow = detail!.Options[0].Values.First(v => v.Value == "Yellow");

        var resolved = await service.ResolveVariantAsync(created.Data!.Id, [yellow.Id]);

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveVariantAsync_ResolvesSingleVariantProductWithEmptySelection()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Single Var", "single-var"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var created = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Simple Product",
            Slug = "simple-product",
            CategoryId = category.Id,
            Price = 50_000m,
            StockQuantity = 7,
            IsActive = true,
            VariantIsActive = true
        });

        var resolved = await service.ResolveVariantAsync(created.Data!.Id, []);

        resolved.Should().NotBeNull();
        resolved!.Price.Should().Be(50_000m);
        resolved.StockQuantity.Should().Be(7);
    }

    [Fact]
    public async Task GetCatalogPagedAsync_FiltersByCategoryAndSearch()
    {
        var catA = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Cat A", "cat-a"));
        var catB = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Cat B", "cat-b"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        await service.CreateAsync(new CreateProductRequest
        {
            Name = "Alpha Headphones",
            Slug = "alpha-headphones",
            CategoryId = catA.Id,
            Price = 100_000m,
            StockQuantity = 5
        });

        await service.CreateAsync(new CreateProductRequest
        {
            Name = "Beta Speaker",
            Slug = "beta-speaker",
            CategoryId = catB.Id,
            Price = 200_000m,
            StockQuantity = 5
        });

        var byCategory = await service.GetCatalogPagedAsync(new CatalogProductQuery
        {
            CategoryId = catA.Id,
            PageSize = 20
        });

        byCategory.Items.Should().Contain(p => p.Slug == "alpha-headphones");
        byCategory.Items.Should().NotContain(p => p.Slug == "beta-speaker");

        var bySearch = await service.GetCatalogPagedAsync(new CatalogProductQuery
        {
            Search = "beta",
            PageSize = 20
        });

        bySearch.Items.Should().ContainSingle(p => p.Slug == "beta-speaker");
    }

    private static async Task<ServiceResult<ProductDetailDto>> SeedIphoneCatalogProductAsync(
        IProductService service,
        int categoryId,
        string slug)
    {
        var product = await service.CreateAsync(new CreateProductRequest
        {
            Name = "iPhone 16",
            Slug = slug,
            CategoryId = categoryId,
            Price = 0,
            StockQuantity = 0,
            IsActive = true,
            VariantIsActive = true
        });

        var saved = await service.SaveOptionsAsync(product.Data!.Id, new SaveProductOptionsRequest
        {
            Options =
            [
                new ProductOptionInput
                {
                    Name = "Color",
                    SortOrder = 0,
                    Values =
                    [
                        new ProductOptionValueInput { Value = "Orange", SortOrder = 0 },
                        new ProductOptionValueInput { Value = "Red", SortOrder = 1 },
                        new ProductOptionValueInput { Value = "Yellow", SortOrder = 2 }
                    ]
                },
                new ProductOptionInput
                {
                    Name = "Storage",
                    SortOrder = 1,
                    Values =
                    [
                        new ProductOptionValueInput { Value = "18GB", SortOrder = 0 },
                        new ProductOptionValueInput { Value = "32GB", SortOrder = 1 }
                    ]
                }
            ]
        });

        await service.GenerateVariantsAsync(product.Data.Id, new GenerateVariantsRequest
        {
            DefaultPrice = 24_990_000m,
            DefaultStock = 10
        });

        var detail = await service.GetByIdAsync(product.Data.Id);
        var yellow32 = detail!.Variants.First(v => v.OptionLabel == "Yellow / 32GB");

        await service.UpsertVariantsAsync(product.Data.Id, new UpsertVariantsRequest
        {
            Variants =
            [
                new VariantUpsertInput
                {
                    Id = yellow32.Id,
                    Sku = yellow32.Sku,
                    Price = 24_990_000m,
                    StockQuantity = 3,
                    IsActive = true,
                    OptionValueIds = yellow32.OptionValueIds
                }
            ]
        });

        return saved;
    }
}
