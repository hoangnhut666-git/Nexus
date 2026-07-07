using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Nexus.Test.Integration.Features.Admin;

public sealed class AdminDashboardPageTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;

    public AdminDashboardPageTests(TestDatabaseFixture fixture)
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
    public async Task GetAdminDashboard_AsAdmin_ReturnsOk()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
        });

        var response = await client.GetAsync("/admin");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAdminDashboard_AsCustomer_ReturnsUnauthorizedOrRedirect()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "/admin");
        request.Headers.Add("X-Test-Roles", IdentitySeedData.CustomerRole);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect,
            HttpStatusCode.Found,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAdminDashboard_Unauthenticated_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var request = new HttpRequestMessage(HttpMethod.Get, "/admin");
        request.Headers.Add("X-Test-Anonymous", "true");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect,
            HttpStatusCode.Found,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found)
        {
            response.Headers.Location?.ToString().Should().Contain("Account/Login");
        }
    }
}
