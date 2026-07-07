using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Services.Cart;
using Nexus.Services.Cart.Models;
using Nexus.Services.Orders;
using Nexus.Services.Orders.Models;
using Nexus.Services.Products;
using Nexus.Services.Products.Models;
using Nexus.Test.Integration.Infrastructure.Auth;
using Nexus.Test.Integration.TestData;

namespace Nexus.Test.Integration.Features.Orders;

public sealed class OrderPageTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;
    private readonly DbHelper _dbHelper;

    public OrderPageTests(TestDatabaseFixture fixture)
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
    public async Task GetCheckoutPage_AsCustomerWithCartItem_ReturnsOk()
    {
        var variant = await SeedVariantAsync("checkout-render", price: 25.00m, stock: 10);
        await AddToCartAsync(TestAuthHandler.TestUserId, variant.Id, 2);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        var request = new HttpRequestMessage(HttpMethod.Get, "/checkout");
        request.Headers.Add("X-Test-Roles", IdentitySeedData.CustomerRole);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Checkout");
        html.Should().Contain("Shipping Address");
    }

    [Fact]
    public async Task GetCheckoutPage_AsNonCustomer_ReturnsRedirectOrForbidden()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Default test identity is an Admin, which lacks the Customer role.
        var response = await client.GetAsync("/checkout");

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect,
            HttpStatusCode.Found,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCheckoutPage_Unauthenticated_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Get, "/checkout");
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
    public async Task GetOrdersPage_AsCustomer_ReturnsOk()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        var request = new HttpRequestMessage(HttpMethod.Get, "/orders");
        request.Headers.Add("X-Test-Roles", IdentitySeedData.CustomerRole);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Your Orders");
    }

    [Fact]
    public async Task GetOrdersPage_Unauthenticated_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Get, "/orders");
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
    public async Task GetOrderDetail_AsCustomerForOwnOrder_ShowsOrder()
    {
        var variant = await SeedVariantAsync("detail-own", price: 30.00m, stock: 10);
        await AddToCartAsync(TestAuthHandler.TestUserId, variant.Id, 1);
        var orderNumber = await PlaceOrderAsync(TestAuthHandler.TestUserId);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        var request = new HttpRequestMessage(HttpMethod.Get, $"/orders/{orderNumber}");
        request.Headers.Add("X-Test-Roles", IdentitySeedData.CustomerRole);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain(orderNumber);
        html.Should().Contain("confirmed");
    }

    [Fact]
    public async Task GetOrderDetail_ForAnotherUsersOrder_ShowsNotFound()
    {
        const string otherUser = "order-page-other-000000000000001";
        await _dbHelper.EnsureUserAsync(otherUser);
        var variant = await SeedVariantAsync("detail-foreign", price: 30.00m, stock: 10);
        await AddToCartAsync(otherUser, variant.Id, 1);
        var orderNumber = await PlaceOrderAsync(otherUser);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        var request = new HttpRequestMessage(HttpMethod.Get, $"/orders/{orderNumber}");
        request.Headers.Add("X-Test-Roles", IdentitySeedData.CustomerRole);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Order not found");
    }

    private async Task AddToCartAsync(string userId, int variantId, int quantity)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var cart = scope.ServiceProvider.GetRequiredService<ICartService>();
        var result = await cart.AddItemAsync(userId, new AddToCartRequest(variantId, quantity));
        result.Success.Should().BeTrue();
    }

    private async Task<string> PlaceOrderAsync(string userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var result = await orders.CreateOrderFromCartAsync(userId, new CheckoutRequest
        {
            ShipFullName = "Jane Buyer",
            ShipPhone = "+1-555-0100",
            ShipStreet = "123 Market St",
            ShipCity = "Springfield",
            ShipState = "IL",
            ShipPostalCode = "62701",
            ShipCountry = "USA",
            Method = Nexus.Data.Entities.PaymentMethod.Cod
        });
        result.Success.Should().BeTrue();
        return result.Data!.OrderNumber;
    }

    private async Task<ProductVariantDto> SeedVariantAsync(string slug, decimal price, int stock)
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug($"Order Page Cat {slug}", $"order-page-cat-{slug}"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

        var created = await productService.CreateAsync(new CreateProductRequest
        {
            Name = $"Order Page Product {slug}",
            Slug = $"order-page-product-{slug}",
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
