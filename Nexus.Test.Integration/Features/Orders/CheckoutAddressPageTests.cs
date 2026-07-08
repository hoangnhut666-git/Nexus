using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Services.Addresses;
using Nexus.Services.Addresses.Models;
using Nexus.Services.Cart;
using Nexus.Services.Cart.Models;
using Nexus.Services.Products;
using Nexus.Services.Products.Models;
using Nexus.Test.Integration.Infrastructure.Auth;
using Nexus.Test.Integration.TestData;

namespace Nexus.Test.Integration.Features.Orders;

public sealed class CheckoutAddressPageTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;
    private readonly DbHelper _dbHelper;

    public CheckoutAddressPageTests(TestDatabaseFixture fixture)
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
    public async Task GetCheckout_WithDefaultAddress_ShowsSavedAddress()
    {
        var variant = await SeedVariantAsync("checkout-default-addr", price: 25.00m, stock: 10);
        await AddToCartAsync(TestAuthHandler.TestUserId, variant.Id, 1);
        await AddAddressAsync("Alice Buyer", setAsDefault: true);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        var request = new HttpRequestMessage(HttpMethod.Get, "/checkout");
        request.Headers.Add("X-Test-Roles", IdentitySeedData.CustomerRole);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Shipping Address");
        html.Should().Contain("Alice Buyer");
        html.Should().Contain("Phường Tân An");
        html.Should().Contain("Tp Cần Thơ");
        html.Should().Contain("Default");
    }

    [Fact]
    public async Task GetCheckout_NoSavedAddress_ShowsNewAddressForm()
    {
        var variant = await SeedVariantAsync("checkout-new-addr", price: 25.00m, stock: 10);
        await AddToCartAsync(TestAuthHandler.TestUserId, variant.Id, 1);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        var request = new HttpRequestMessage(HttpMethod.Get, "/checkout");
        request.Headers.Add("X-Test-Roles", IdentitySeedData.CustomerRole);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Shipping Address");
        html.Should().Contain("Ward / Commune");
        html.Should().Contain("Province / City");
        html.Should().Contain("Save this address to my account");
        // Province dropdown is rendered from the bundled dataset.
        html.Should().Contain("Hà Nội");
    }

    private async Task AddToCartAsync(string userId, int variantId, int quantity)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var cart = scope.ServiceProvider.GetRequiredService<ICartService>();
        var result = await cart.AddItemAsync(userId, new AddToCartRequest(variantId, quantity));
        result.Success.Should().BeTrue();
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
            Ward = "Phường Tân An",
            Province = "Tp Cần Thơ",
            Country = "Vietnam",
            SetAsDefault = setAsDefault
        });

        result.Success.Should().BeTrue();
        return result.Data!;
    }

    private async Task<ProductVariantDto> SeedVariantAsync(string slug, decimal price, int stock)
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug($"Checkout Cat {slug}", $"checkout-cat-{slug}"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

        var created = await productService.CreateAsync(new CreateProductRequest
        {
            Name = $"Checkout Product {slug}",
            Slug = $"checkout-product-{slug}",
            CategoryId = category.Id,
            Price = price,
            StockQuantity = stock,
            IsActive = true,
            VariantIsActive = true
        });

        created.Success.Should().BeTrue();
        return created.Data!.Variants[0];
    }
}
