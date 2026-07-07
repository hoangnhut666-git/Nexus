using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Nexus.Test.Integration.Features.Category;

public sealed class CategoryPageTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;

    public CategoryPageTests(TestDatabaseFixture fixture)
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
    public async Task GetCategoriesPage_AsAdmin_ReturnsOk()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
        });

        var response = await client.GetAsync("/admin/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCategoriesPage_AsCustomer_ReturnsUnauthorizedOrRedirect()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/categories");
        request.Headers.Add("X-Test-Roles", IdentitySeedData.CustomerRole);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect,
            HttpStatusCode.Found,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }
}
