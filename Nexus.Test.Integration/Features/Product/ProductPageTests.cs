using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Nexus.Test.Integration.Features.Product;

public sealed class ProductPageTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;

    public ProductPageTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _factory = new TestWebApplicationFactory(fixture.ConnectionString);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ResetAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetProductsPage_AsAdmin_ReturnsOk()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
        });

        var response = await client.GetAsync("/admin/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProductsPage_AsCustomer_ReturnsUnauthorizedOrRedirect()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/products");
        request.Headers.Add("X-Test-Roles", IdentitySeedData.CustomerRole);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect,
            HttpStatusCode.Found,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }
}
