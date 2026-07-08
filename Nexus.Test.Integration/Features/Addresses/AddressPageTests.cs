using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Services.Addresses;
using Nexus.Services.Addresses.Models;
using Nexus.Test.Integration.Infrastructure.Auth;

namespace Nexus.Test.Integration.Features.Addresses;

public sealed class AddressPageTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;
    private readonly DbHelper _dbHelper;

    public AddressPageTests(TestDatabaseFixture fixture)
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
    public async Task GetAddresses_Unauthenticated_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Get, "/Account/Manage/Addresses");
        request.Headers.Add("X-Test-Anonymous", "true");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect,
            HttpStatusCode.Found,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found)
            response.Headers.Location?.ToString().Should().Contain("Account/Login");
    }

    [Fact]
    public async Task GetAddresses_Authenticated_ShowsSavedAddress()
    {
        await AddAddressAsync("Alice Buyer", setAsDefault: true);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        var response = await client.GetAsync("/Account/Manage/Addresses");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Shipping Addresses");
        html.Should().Contain("Alice Buyer");
        html.Should().Contain("Tan An");
        html.Should().Contain("Can Tho");
        html.Should().Contain("Default");
    }

    [Fact]
    public async Task GetAddresses_Empty_ShowsEmptyState()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        var response = await client.GetAsync("/Account/Manage/Addresses");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("no saved addresses");
        html.Should().Contain("Account/Manage/Addresses/Add");
    }

    [Fact]
    public async Task GetAddPage_Authenticated_ReturnsForm()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        var response = await client.GetAsync("/Account/Manage/Addresses/Add");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Add address");
        html.Should().Contain("Recipient name");
        html.Should().Contain("Ward / Commune");
        html.Should().Contain("Province / City");
    }

    [Fact]
    public async Task GetEditPage_ForOwnAddress_ReturnsPrefilledForm()
    {
        var created = await AddAddressAsync("Alice Buyer", setAsDefault: false);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        var response = await client.GetAsync($"/Account/Manage/Addresses/Edit/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Edit address");
        html.Should().Contain("Alice Buyer");
    }

    private async Task<AddressDto> AddAddressAsync(string recipient, bool setAsDefault)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IAddressService>();
        var result = await service.AddAsync(TestAuthHandler.TestUserId, new AddressInput
        {
            RecipientName = recipient,
            Phone = "0901234567",
            AddressLine = "No. 25, Main Street",
            Ward = "Tan An",
            Province = "Can Tho",
            Country = "Vietnam",
            SetAsDefault = setAsDefault
        });

        result.Success.Should().BeTrue();
        return result.Data!;
    }
}
