using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Services.Categories.Models;
using Nexus.Services.Products;
using Nexus.Services.Products.Models;
using Nexus.Test.Integration.TestData;

namespace Nexus.Test.Integration.Features.Product;

public sealed class ProductCatalogPageTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly DbHelper _dbHelper;

    public ProductCatalogPageTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _factory = new TestWebApplicationFactory(fixture.ConnectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
        });
        _dbHelper = new DbHelper(fixture.ConnectionString);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ResetAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetProductsPage_ReturnsOkWithoutAuth()
    {
        var response = await _client.GetAsync("/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProductsPage_ContainsCatalogHeading()
    {
        var response = await _client.GetAsync("/products");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("Shop");
    }

    [Fact]
    public async Task GetProductDetailPage_ActiveProduct_ReturnsOk()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Page Cat", "page-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        await service.CreateAsync(new CreateProductRequest
        {
            Name = "Catalog Page Product",
            Slug = "catalog-page-product",
            CategoryId = category.Id,
            Price = 150_000m,
            StockQuantity = 5,
            IsActive = true,
            VariantIsActive = true
        });

        var response = await _client.GetAsync("/products/catalog-page-product");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Catalog Page Product");
    }

    [Fact]
    public async Task GetProductDetailPage_InactiveProduct_ShowsNotFoundContent()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Inactive Page", "inactive-page"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();

        await service.CreateAsync(new CreateProductRequest
        {
            Name = "Hidden Catalog Product",
            Slug = "hidden-catalog-product",
            CategoryId = category.Id,
            Price = 100_000m,
            StockQuantity = 1,
            IsActive = false,
            VariantIsActive = true
        });

        var response = await _client.GetAsync("/products/hidden-catalog-product");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Product not found");
    }

    [Fact]
    public async Task GetProductDetailPage_UnknownSlug_ShowsNotFoundContent()
    {
        var response = await _client.GetAsync("/products/does-not-exist-slug");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Product not found");
    }
}
