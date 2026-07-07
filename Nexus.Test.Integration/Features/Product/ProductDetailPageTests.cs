using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Services.Products;
using Nexus.Services.Products.Models;
using Nexus.Test.Integration.TestData;

namespace Nexus.Test.Integration.Features.Product;

public sealed class ProductDetailPageTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;
    private readonly DbHelper _dbHelper;

    public ProductDetailPageTests(TestDatabaseFixture fixture)
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
    public async Task GetProductDetailPage_AsAdmin_ReturnsOk()
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug("Detail Cat", "detail-cat"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();
        var created = await service.CreateAsync(ProductTestDataBuilder.ValidCreateProductRequest(category.Id));

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
        });

        var response = await client.GetAsync($"/admin/products/{created.Data!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
