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

public sealed class AdminOrderPageTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;
    private readonly DbHelper _dbHelper;

    public AdminOrderPageTests(TestDatabaseFixture fixture)
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
    public async Task GetAdminOrders_AsAdmin_ReturnsOk()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });

        // Default test identity is an Admin.
        var response = await client.GetAsync("/admin/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Search order # or customer");
    }

    [Fact]
    public async Task GetAdminOrders_AsCustomer_ReturnsRedirectOrForbidden()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/orders");
        request.Headers.Add("X-Test-Roles", IdentitySeedData.CustomerRole);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect,
            HttpStatusCode.Found,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAdminOrders_Unauthenticated_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/orders");
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
    public async Task GetAdminOrderDetail_AsAdmin_ShowsOrder()
    {
        const string customer = "admin-page-customer-000000000001";
        await _dbHelper.EnsureUserAsync(customer);
        var variant = await SeedVariantAsync("admin-detail", price: 30.00m, stock: 10);
        var orderNumber = await PlaceOrderAsync(customer, variant.Id, 1);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });

        var response = await client.GetAsync($"/admin/orders/{orderNumber}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain(orderNumber);
    }

    private async Task<string> PlaceOrderAsync(string userId, int variantId, int quantity)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var cart = scope.ServiceProvider.GetRequiredService<ICartService>();
        var addResult = await cart.AddItemAsync(userId, new AddToCartRequest(variantId, quantity));
        addResult.Success.Should().BeTrue();

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
            Method = PaymentMethod.Cod
        });

        result.Success.Should().BeTrue();
        return result.Data!.OrderNumber;
    }

    private async Task<ProductVariantDto> SeedVariantAsync(string slug, decimal price, int stock)
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug($"Admin Page Cat {slug}", $"admin-page-cat-{slug}"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

        var created = await productService.CreateAsync(new CreateProductRequest
        {
            Name = $"Admin Page Product {slug}",
            Slug = $"admin-page-product-{slug}",
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
