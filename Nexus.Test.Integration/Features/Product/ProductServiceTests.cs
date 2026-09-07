using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Services.Products;
using Nexus.Services.Products.Models;
using Nexus.Test.Integration.TestData;

namespace Nexus.Test.Integration.Features.Product;

public sealed class ProductServiceTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;
    private readonly DbHelper _dbHelper;

    public ProductServiceTests(TestDatabaseFixture fixture)
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
    public async Task CreateAsync_PersistsProductWithDefaultVariant()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Mechanical Keyboards", "mechanical-keyboards"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var result = await service.CreateAsync(ProductTestDataBuilder.ValidCreateProductRequest(category.Id));

        result.Success.Should().BeTrue();
        result.Data!.Name.Should().Be("CyberType S87 Keyboard");
        result.Data.Variants.Should().HaveCount(1);
        result.Data.Variants[0].Price.Should().Be(2_450_000m);
        result.Data.Variants[0].StockQuantity.Should().Be(124);

        var variants = await _dbHelper.GetProductVariantsAsync(result.Data.Id);
        variants.Should().HaveCount(1);
        variants[0].Sku.Should().EndWith("-default");
    }

    [Fact]
    public async Task CreateAsync_AutoGeneratesSlug_WhenOmitted()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Audio", "premium-audio"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var result = await service.CreateAsync(new Nexus.Services.Products.Models.CreateProductRequest
        {
            Name = "Nexus Sound Pro X",
            CategoryId = category.Id,
            Price = 3_200_000m,
            StockQuantity = 10
        });

        result.Success.Should().BeTrue();
        result.Data!.Slug.Should().Be("nexus-sound-pro-x");

        var persisted = await _dbHelper.GetProductBySlugAsync("nexus-sound-pro-x");
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_DisambiguatesSlug_WhenDuplicate()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Phones", "phones"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var first = await service.CreateAsync(new Nexus.Services.Products.Models.CreateProductRequest
        {
            Name = "iPhone 16",
            CategoryId = category.Id,
            Price = 24_990_000m,
            StockQuantity = 5
        });

        var second = await service.CreateAsync(new Nexus.Services.Products.Models.CreateProductRequest
        {
            Name = "iPhone 16",
            CategoryId = category.Id,
            Price = 24_990_000m,
            StockQuantity = 3
        });

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        first.Data!.Slug.Should().Be("iphone-16");
        second.Data!.Slug.Should().Be("iphone-16-2");
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateSku_ReturnsFailure()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Accessories", "accessories"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        await service.CreateAsync(ProductTestDataBuilder.ValidCreateProductRequestWithSku(category.Id, "NX-DUP-SKU"));

        var duplicate = await service.CreateAsync(ProductTestDataBuilder.ValidCreateProductRequestWithSku(category.Id, "NX-DUP-SKU"));

        duplicate.Success.Should().BeFalse();
        duplicate.Error.Should().Contain("SKU");
    }

    [Fact]
    public async Task CreateAsync_WithInvalidCategory_ReturnsFailure()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var result = await service.CreateAsync(new Nexus.Services.Products.Models.CreateProductRequest
        {
            Name = "Orphan Product",
            CategoryId = 999_999,
            Price = 100_000m,
            StockQuantity = 1
        });

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Category");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsProductWithVariants()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Gaming", "gaming"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var created = await service.CreateAsync(ProductTestDataBuilder.ValidCreateProductRequest(category.Id));
        var detail = await service.GetByIdAsync(created.Data!.Id);

        detail.Should().NotBeNull();
        detail!.CategoryName.Should().Be("Gaming");
        detail.Variants.Should().HaveCount(1);
        detail.Variants[0].Sku.Should().Be(created.Data.Variants[0].Sku);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var detail = await service.GetByIdAsync(999_999);

        detail.Should().BeNull();
    }

    [Fact]
    public void GenerateSlug_HandlesVietnameseDiacritics()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        service.GenerateSlug("Điện thoại thông minh").Should().Be("dien-thoai-thong-minh");
    }

    [Fact]
    public async Task SaveUploadedFileAsync_SavesFileUnderProductsUploads()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var imageService = scope.ServiceProvider.GetRequiredService<IProductImageService>();

        await using var stream = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var result = await imageService.SaveUploadedFileAsync(stream, "icon.png", "image/png", stream.Length);

        result.Success.Should().BeTrue();
        result.Data.Should().StartWith("/uploads/products/");

        var relativePath = result.Data!.TrimStart('/');
        var fullPath = Path.Combine(
            _factory.Server.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(fullPath).Should().BeTrue();

        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersBySearchName()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Search Cat", "search-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        await service.CreateAsync(new CreateProductRequest
        {
            Name = "Unique Alpha Product",
            Slug = "unique-alpha",
            CategoryId = category.Id,
            Sku = "NX-ALPHA",
            Price = 100_000m,
            StockQuantity = 1
        });
        await service.CreateAsync(new CreateProductRequest
        {
            Name = "Other Product",
            Slug = "other-product",
            CategoryId = category.Id,
            Sku = "NX-OTHER",
            Price = 200_000m,
            StockQuantity = 2
        });

        var result = await service.GetPagedAsync(new ProductQuery
        {
            Search = "alpha",
            Status = ProductStatusFilter.All,
            PageSize = 20
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Unique Alpha Product");
    }

    [Fact]
    public async Task GetPagedAsync_FiltersBySearchSku()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("SKU Cat", "sku-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        await service.CreateAsync(new CreateProductRequest
        {
            Name = "Product A",
            Slug = "product-a",
            CategoryId = category.Id,
            Sku = "NX-SPECIAL-999",
            Price = 100_000m,
            StockQuantity = 1
        });

        var result = await service.GetPagedAsync(new ProductQuery
        {
            Search = "SPECIAL-999",
            Status = ProductStatusFilter.All,
            PageSize = 20
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Product A");
        result.Items[0].UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByCategory()
    {
        var catA = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Cat A", "cat-a"));
        var catB = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Cat B", "cat-b"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        await service.CreateAsync(new CreateProductRequest
        {
            Name = "In Cat A",
            Slug = "in-cat-a",
            CategoryId = catA.Id,
            Sku = "NX-A1",
            Price = 100_000m,
            StockQuantity = 1
        });
        await service.CreateAsync(new CreateProductRequest
        {
            Name = "In Cat B",
            Slug = "in-cat-b",
            CategoryId = catB.Id,
            Sku = "NX-B1",
            Price = 100_000m,
            StockQuantity = 1
        });

        var result = await service.GetPagedAsync(new ProductQuery
        {
            CategoryId = catA.Id,
            Status = ProductStatusFilter.All,
            PageSize = 20
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].CategoryName.Should().Be("Cat A");
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByStatus()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Status Cat", "status-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        await service.CreateAsync(new CreateProductRequest
        {
            Name = "Visible Product",
            Slug = "visible-product",
            CategoryId = category.Id,
            Sku = "NX-VIS",
            Price = 100_000m,
            StockQuantity = 1,
            IsActive = true
        });
        var hidden = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Hidden Product",
            Slug = "hidden-product",
            CategoryId = category.Id,
            Sku = "NX-HID",
            Price = 100_000m,
            StockQuantity = 1,
            IsActive = false
        });

        hidden.Success.Should().BeTrue();

        var active = await service.GetPagedAsync(new ProductQuery { Status = ProductStatusFilter.Active, PageSize = 20 });
        var hiddenOnly = await service.GetPagedAsync(new ProductQuery { Status = ProductStatusFilter.Hidden, PageSize = 20 });

        active.Items.Should().Contain(p => p.Slug == "visible-product");
        active.Items.Should().NotContain(p => p.Slug == "hidden-product");
        hiddenOnly.Items.Should().Contain(p => p.Slug == "hidden-product");
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsSecondPage()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Pag Cat", "pag-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        for (var i = 1; i <= 8; i++)
        {
            await service.CreateAsync(new CreateProductRequest
            {
                Name = $"Product {i:D2}",
                Slug = $"product-{i:D2}",
                CategoryId = category.Id,
                Sku = $"NX-P{i:D2}",
                Price = 100_000m,
                StockQuantity = 1,
                IsActive = true
            });
        }

        var page1 = await service.GetPagedAsync(new ProductQuery
        {
            Page = 1,
            PageSize = 6,
            Status = ProductStatusFilter.All
        });
        var page2 = await service.GetPagedAsync(new ProductQuery
        {
            Page = 2,
            PageSize = 6,
            Status = ProductStatusFilter.All
        });

        page1.Items.Should().HaveCount(6);
        page2.Items.Should().HaveCount(2);
        page2.TotalCount.Should().Be(8);
    }

    [Fact]
    public async Task GetPagedAsync_AggregatesPriceAndStock()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Agg Cat", "agg-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var product = await _dbHelper.InsertProductAsync(
            ProductTestDataBuilder.ValidProductWithSlug(category.Id, "Multi Variant", "multi-variant"));
        await _dbHelper.InsertProductVariantAsync(product.Id, "NX-MV-1", 1_000_000m, 10);
        await _dbHelper.InsertProductVariantAsync(product.Id, "NX-MV-2", 2_000_000m, 5);

        var result = await service.GetPagedAsync(new ProductQuery
        {
            Search = "multi-variant",
            Status = ProductStatusFilter.All,
            PageSize = 20
        });

        result.Items.Should().HaveCount(1);
        result.Items[0].Price.Should().Be(1_000_000m);
        result.Items[0].TotalStock.Should().Be(15);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesProductShellOnly()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Update Cat", "update-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var created = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Original Name",
            Slug = "original-name",
            CategoryId = category.Id,
            Sku = "NX-ORIG",
            Price = 500_000m,
            StockQuantity = 10
        });

        var result = await service.UpdateAsync(created.Data!.Id, new UpdateProductRequest
        {
            Name = "Updated Name",
            Slug = "updated-name",
            Description = "New description",
            CategoryId = category.Id,
            IsActive = true
        });

        result.Success.Should().BeTrue();
        result.Data!.Name.Should().Be("Updated Name");
        result.Data.Variants[0].Sku.Should().Be("NX-ORIG");
        result.Data.Variants[0].Price.Should().Be(500_000m);
        result.Data.Variants[0].StockQuantity.Should().Be(10);
    }

    [Fact]
    public async Task UpsertVariantsAsync_WithDuplicateSku_ReturnsFailure()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Dup SKU Cat", "dup-sku-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var first = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Product One",
            Slug = "product-one",
            CategoryId = category.Id,
            Sku = "NX-TAKEN",
            Price = 100_000m,
            StockQuantity = 1
        });
        var second = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Product Two",
            Slug = "product-two",
            CategoryId = category.Id,
            Sku = "NX-MINE",
            Price = 100_000m,
            StockQuantity = 1
        });

        var result = await service.UpsertVariantsAsync(second.Data!.Id, new UpsertVariantsRequest
        {
            Variants =
            [
                new VariantUpsertInput
                {
                    Id = second.Data.Variants[0].Id,
                    Sku = "NX-TAKEN",
                    Price = 100_000m,
                    StockQuantity = 1
                }
            ]
        });

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("SKU");
    }

    [Fact]
    public async Task SetActiveAsync_TogglesIsActive()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Toggle Cat", "toggle-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var created = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Toggle Product",
            Slug = "toggle-product",
            CategoryId = category.Id,
            Sku = "NX-TOG",
            Price = 100_000m,
            StockQuantity = 1,
            IsActive = true
        });

        var hide = await service.SetActiveAsync(created.Data!.Id, false);
        hide.Success.Should().BeTrue();

        var hidden = await service.GetByIdAsync(created.Data.Id);
        hidden!.IsActive.Should().BeFalse();

        var show = await service.SetActiveAsync(created.Data.Id, true);
        show.Success.Should().BeTrue();

        var visible = await service.GetByIdAsync(created.Data.Id);
        visible!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SaveOptionsAsync_PersistsColorAndStorage()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Options Cat", "options-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var product = await service.CreateAsync(new CreateProductRequest
        {
            Name = "iPhone 16",
            Slug = "iphone-16",
            CategoryId = category.Id,
            Price = 0,
            StockQuantity = 0
        });

        var result = await service.SaveOptionsAsync(product.Data!.Id, new SaveProductOptionsRequest
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

        result.Success.Should().BeTrue();
        result.Data!.Options.Should().HaveCount(2);
        result.Data.Options[0].Name.Should().Be("Color");
        result.Data.Options[0].Values.Should().HaveCount(3);
        result.Data.Options[1].Name.Should().Be("Storage");
        result.Data.Options[1].Values.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveOptionsAsync_BlocksDeleteOfReferencedValue()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Block Cat", "block-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var product = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Test Phone",
            Slug = "test-phone",
            CategoryId = category.Id,
            Price = 0,
            StockQuantity = 0
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
                        new ProductOptionValueInput { Value = "Red", SortOrder = 0 },
                        new ProductOptionValueInput { Value = "Blue", SortOrder = 1 }
                    ]
                }
            ]
        });

        await service.GenerateVariantsAsync(product.Data.Id, new GenerateVariantsRequest
        {
            DefaultPrice = 100_000m,
            DefaultStock = 5
        });

        var colorOption = saved.Data!.Options[0];
        var redValue = colorOption.Values.First(v => v.Value == "Red");

        var blocked = await service.SaveOptionsAsync(product.Data.Id, new SaveProductOptionsRequest
        {
            Options =
            [
                new ProductOptionInput
                {
                    Id = colorOption.Id,
                    Name = "Color",
                    SortOrder = 0,
                    Values =
                    [
                        new ProductOptionValueInput
                        {
                            Id = colorOption.Values.First(v => v.Value == "Blue").Id,
                            Value = "Blue",
                            SortOrder = 0
                        }
                    ]
                }
            ]
        });

        blocked.Success.Should().BeFalse();
        blocked.Error.Should().Contain("Red");
    }

    [Fact]
    public async Task GenerateVariantsAsync_CreatesCartesianProduct()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Cartesian Cat", "cartesian-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var product = await service.CreateAsync(new CreateProductRequest
        {
            Name = "iPhone 16",
            Slug = "iphone-16-cart",
            CategoryId = category.Id,
            Price = 0,
            StockQuantity = 0
        });

        await service.SaveOptionsAsync(product.Data!.Id, new SaveProductOptionsRequest
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

        var generated = await service.GenerateVariantsAsync(product.Data.Id, new GenerateVariantsRequest
        {
            DefaultPrice = 24_990_000m,
            DefaultStock = 10
        });

        generated.Success.Should().BeTrue();
        generated.Data.Should().HaveCount(6);

        var detail = await service.GetByIdAsync(product.Data.Id);
        detail!.Variants.Should().HaveCount(7);
    }

    [Fact]
    public async Task GenerateVariantsAsync_SkipsExistingCombinations()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Skip Cat", "skip-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var product = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Combo Phone",
            Slug = "combo-phone",
            CategoryId = category.Id,
            Price = 0,
            StockQuantity = 0
        });

        await service.SaveOptionsAsync(product.Data!.Id, new SaveProductOptionsRequest
        {
            Options =
            [
                new ProductOptionInput
                {
                    Name = "Color",
                    SortOrder = 0,
                    Values =
                    [
                        new ProductOptionValueInput { Value = "Red", SortOrder = 0 },
                        new ProductOptionValueInput { Value = "Blue", SortOrder = 1 }
                    ]
                },
                new ProductOptionInput
                {
                    Name = "Size",
                    SortOrder = 1,
                    Values =
                    [
                        new ProductOptionValueInput { Value = "S", SortOrder = 0 },
                        new ProductOptionValueInput { Value = "M", SortOrder = 1 }
                    ]
                }
            ]
        });

        var first = await service.GenerateVariantsAsync(product.Data.Id, new GenerateVariantsRequest
        {
            DefaultPrice = 100_000m,
            DefaultStock = 1
        });
        var second = await service.GenerateVariantsAsync(product.Data.Id, new GenerateVariantsRequest
        {
            DefaultPrice = 100_000m,
            DefaultStock = 1
        });

        first.Success.Should().BeTrue();
        first.Data.Should().HaveCount(4);
        second.Success.Should().BeTrue();
        second.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateVariantsAsync_RejectsDuplicateSku()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Dup Gen Cat", "dup-gen-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var product = await service.CreateAsync(new CreateProductRequest
        {
            Name = "SKU Phone",
            Slug = "sku-phone",
            CategoryId = category.Id,
            Price = 0,
            StockQuantity = 0
        });

        var saved = await service.SaveOptionsAsync(product.Data!.Id, new SaveProductOptionsRequest
        {
            Options =
            [
                new ProductOptionInput
                {
                    Name = "Color",
                    SortOrder = 0,
                    Values = [new ProductOptionValueInput { Value = "Red", SortOrder = 0 }]
                }
            ]
        });

        var expectedSku = ProductSkuHelper.GenerateSku("sku-phone", ["Red"]);

        var otherProduct = await _dbHelper.InsertProductAsync(
            ProductTestDataBuilder.ValidProductWithSlug(category.Id, "Other Product", "other-sku-product"));
        await _dbHelper.InsertProductVariantAsync(otherProduct.Id, expectedSku, 50_000m, 1);

        var generated = await service.GenerateVariantsAsync(product.Data.Id, new GenerateVariantsRequest
        {
            DefaultPrice = 100_000m,
            DefaultStock = 1
        });

        generated.Success.Should().BeFalse();
        generated.Error.Should().Contain("SKU");
    }

    [Fact]
    public async Task UpsertVariantsAsync_CreatesVariantForSubset()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Subset Cat", "subset-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var product = await service.CreateAsync(new CreateProductRequest
        {
            Name = "iPhone 16",
            Slug = "iphone-16-subset",
            CategoryId = category.Id,
            Price = 0,
            StockQuantity = 0
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

        var colorValues = saved.Data!.Options[0].Values.ToDictionary(v => v.Value, v => v.Id);
        var storageValues = saved.Data.Options[1].Values.ToDictionary(v => v.Value, v => v.Id);

        var combos = new (string Sku, string Color, string Storage, decimal Price, int Stock)[]
        {
            ("IPH16-ORG-32", "Orange", "32GB", 24_990_000m, 12),
            ("IPH16-RED-32", "Red", "32GB", 24_990_000m, 8),
            ("IPH16-YEL-18", "Yellow", "18GB", 22_990_000m, 5),
            ("IPH16-YEL-32", "Yellow", "32GB", 24_990_000m, 3)
        };

        var result = await service.UpsertVariantsAsync(product.Data.Id, new UpsertVariantsRequest
        {
            Variants = combos.Select(c => new VariantUpsertInput
            {
                Sku = c.Sku,
                Price = c.Price,
                StockQuantity = c.Stock,
                IsActive = true,
                OptionValueIds = [colorValues[c.Color], storageValues[c.Storage]]
            }).ToList()
        });

        result.Success.Should().BeTrue();
        result.Data!.Variants.Should().HaveCount(5);
        result.Data.Variants.Count(v => v.OptionValueIds.Count == 2).Should().Be(4);
    }

    [Fact]
    public async Task UpsertVariantsAsync_DeletesVariant()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Delete Cat", "delete-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var product = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Delete Variants",
            Slug = "delete-variants",
            CategoryId = category.Id,
            Price = 0,
            StockQuantity = 0
        });

        await service.SaveOptionsAsync(product.Data!.Id, new SaveProductOptionsRequest
        {
            Options =
            [
                new ProductOptionInput
                {
                    Name = "Color",
                    SortOrder = 0,
                    Values =
                    [
                        new ProductOptionValueInput { Value = "Red", SortOrder = 0 },
                        new ProductOptionValueInput { Value = "Blue", SortOrder = 1 }
                    ]
                }
            ]
        });

        var generated = await service.GenerateVariantsAsync(product.Data.Id, new GenerateVariantsRequest
        {
            DefaultPrice = 100_000m,
            DefaultStock = 10
        });

        var toDelete = generated.Data!.First().Id;

        var result = await service.UpsertVariantsAsync(product.Data.Id, new UpsertVariantsRequest
        {
            DeleteVariantIds = [toDelete]
        });

        result.Success.Should().BeTrue();
        result.Data!.Variants.Should().HaveCount(2);

        var paged = await service.GetPagedAsync(new ProductQuery
        {
            Search = "delete-variants",
            Status = ProductStatusFilter.All,
            PageSize = 20
        });

        paged.Items.Should().HaveCount(1);
        paged.Items[0].TotalStock.Should().Be(10);
    }

    [Fact]
    public async Task GetByIdAsync_IncludesOptionsAndVariantLabels()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Label Cat", "label-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var product = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Label Phone",
            Slug = "label-phone",
            CategoryId = category.Id,
            Price = 0,
            StockQuantity = 0
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
                        new ProductOptionValueInput { Value = "Yellow", SortOrder = 0 }
                    ]
                },
                new ProductOptionInput
                {
                    Name = "Storage",
                    SortOrder = 1,
                    Values =
                    [
                        new ProductOptionValueInput { Value = "32GB", SortOrder = 0 }
                    ]
                }
            ]
        });

        var yellowId = saved.Data!.Options[0].Values[0].Id;
        var storageId = saved.Data.Options[1].Values[0].Id;

        await service.UpsertVariantsAsync(product.Data.Id, new UpsertVariantsRequest
        {
            Variants =
            [
                new VariantUpsertInput
                {
                    Sku = "LBL-YEL-32",
                    Price = 1_000_000m,
                    StockQuantity = 3,
                    OptionValueIds = [yellowId, storageId]
                }
            ]
        });

        var detail = await service.GetByIdAsync(product.Data.Id);

        detail!.Options.Should().HaveCount(2);
        var labeled = detail.Variants.First(v => v.Sku == "LBL-YEL-32");
        labeled.OptionLabel.Should().Be("Yellow / 32GB");
    }

    [Fact]
    public async Task SetActiveAsync_RequiresActiveVariant()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Active Cat", "active-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var product = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Inactive Variants",
            Slug = "inactive-variants",
            CategoryId = category.Id,
            Sku = "NX-INACT",
            Price = 100_000m,
            StockQuantity = 1,
            IsActive = false
        });

        await service.BulkUpdateVariantsAsync(product.Data!.Id, new BulkVariantActionRequest
        {
            VariantIds = [product.Data.Variants[0].Id],
            Action = BulkVariantAction.SetActive,
            IsActive = false
        });

        var activate = await service.SetActiveAsync(product.Data.Id, true);

        activate.Success.Should().BeFalse();
        activate.Error.Should().Contain("active variant");
    }

    [Fact]
    public async Task GetPagedAsync_StillAggregatesMultiVariantProduct()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Multi Cat", "multi-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var product = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Multi Option Product",
            Slug = "multi-option-product",
            CategoryId = category.Id,
            Price = 0,
            StockQuantity = 0
        });

        await service.SaveOptionsAsync(product.Data!.Id, new SaveProductOptionsRequest
        {
            Options =
            [
                new ProductOptionInput
                {
                    Name = "Tier",
                    SortOrder = 0,
                    Values =
                    [
                        new ProductOptionValueInput { Value = "Basic", SortOrder = 0 },
                        new ProductOptionValueInput { Value = "Pro", SortOrder = 1 }
                    ]
                }
            ]
        });

        await service.GenerateVariantsAsync(product.Data.Id, new GenerateVariantsRequest
        {
            DefaultPrice = 500_000m,
            DefaultStock = 4
        });

        var detail = await service.GetByIdAsync(product.Data.Id);
        var defaultVariant = detail!.Variants.Single(v => v.OptionValueIds.Count == 0);

        await service.UpsertVariantsAsync(product.Data.Id, new UpsertVariantsRequest
        {
            DeleteVariantIds = [defaultVariant.Id]
        });

        var afterDelete = await service.GetByIdAsync(product.Data.Id);
        var cheapest = afterDelete!.Variants.OrderBy(v => v.Price).First();
        await service.UpsertVariantsAsync(product.Data.Id, new UpsertVariantsRequest
        {
            Variants =
            [
                new VariantUpsertInput
                {
                    Id = cheapest.Id,
                    Sku = cheapest.Sku,
                    Price = 300_000m,
                    StockQuantity = cheapest.StockQuantity,
                    OptionValueIds = cheapest.OptionValueIds
                }
            ]
        });

        var paged = await service.GetPagedAsync(new ProductQuery
        {
            Search = "multi-option-product",
            Status = ProductStatusFilter.All,
            PageSize = 20
        });

        paged.Items.Should().HaveCount(1);
        paged.Items[0].Price.Should().Be(300_000m);
        paged.Items[0].TotalStock.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BulkUpdateVariantsAsync_AdjustsStockForSelectedVariants()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Bulk Cat", "bulk-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        var product = await service.CreateAsync(new CreateProductRequest
        {
            Name = "Bulk Product",
            Slug = "bulk-product",
            CategoryId = category.Id,
            Price = 0,
            StockQuantity = 0
        });

        await service.SaveOptionsAsync(product.Data!.Id, new SaveProductOptionsRequest
        {
            Options =
            [
                new ProductOptionInput
                {
                    Name = "Color",
                    SortOrder = 0,
                    Values =
                    [
                        new ProductOptionValueInput { Value = "Red", SortOrder = 0 },
                        new ProductOptionValueInput { Value = "Blue", SortOrder = 1 }
                    ]
                }
            ]
        });

        var generated = await service.GenerateVariantsAsync(product.Data.Id, new GenerateVariantsRequest
        {
            DefaultPrice = 100_000m,
            DefaultStock = 5
        });

        var targetIds = generated.Data!.Select(v => v.Id).ToList();

        var result = await service.BulkUpdateVariantsAsync(product.Data.Id, new BulkVariantActionRequest
        {
            VariantIds = targetIds,
            Action = BulkVariantAction.AdjustStock,
            StockDelta = 3
        });

        result.Success.Should().BeTrue();
        result.Data.Should().Be(2);

        var detail = await service.GetByIdAsync(product.Data.Id);
        detail!.Variants.Where(v => targetIds.Contains(v.Id)).Should().OnlyContain(v => v.StockQuantity == 8);
    }
}
