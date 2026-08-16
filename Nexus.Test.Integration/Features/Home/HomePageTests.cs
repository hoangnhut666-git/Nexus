using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Nexus.Test.Integration.Features.Home;

/// <summary>
/// HTTP-level smoke tests that verify the Blazor app boots in-process.
/// </summary>
public sealed class HomePageTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public HomePageTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _factory = new TestWebApplicationFactory(fixture.ConnectionString);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ResetAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetHomePage_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetHomePage_ContainsStorefrontContent()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("NEXUS");
        html.Should().Contain("Trending Categories");
        html.Should().Contain("Hottest Products");
        html.Should().Contain("Join the NEXUS Club");
    }

    [Fact]
    public async Task GetHomePage_ContainsThemeToggleAndBootScript()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        html.Should().Contain("data-theme-toggle");
        html.Should().Contain("data-testid=\"theme-toggle\"");
        html.Should().Contain("nexus-theme");
        html.Should().Contain("js/theme.js");
    }
}
