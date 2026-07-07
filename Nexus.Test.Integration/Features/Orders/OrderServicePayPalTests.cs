using Microsoft.Extensions.DependencyInjection;
using Nexus.Services.Cart;
using Nexus.Services.Cart.Models;
using Nexus.Services.Orders;
using Nexus.Services.Orders.Models;
using Nexus.Services.Products;
using Nexus.Services.Products.Models;
using Nexus.Test.Integration.TestData;

namespace Nexus.Test.Integration.Features.Orders;

public sealed class OrderServicePayPalTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private const string UserA = "paypal-user-a-000000000000001";
    private const string UserB = "paypal-user-b-000000000000002";
    private const string ReturnBase = "https://shop.test/";

    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;
    private readonly DbHelper _dbHelper;

    public OrderServicePayPalTests(TestDatabaseFixture fixture)
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
    public async Task Checkout_PayPal_CreatesPendingOrderKeepsCartAndDeductsStock()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("pp-checkout", price: 20.00m, stock: 10);
        await AddToCartAsync(UserA, variant.Id, 2);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var result = await orders.CreateOrderFromCartAsync(UserA, PayPalCheckout());

        result.Success.Should().BeTrue();
        result.Data!.RedirectUrl.Should().NotBeNullOrWhiteSpace();

        var order = await _dbHelper.GetOrderByNumberAsync(result.Data.OrderNumber);
        order.Should().NotBeNull();
        order!.Status.Should().Be(OrderStatus.Pending);
        order.PaymentMethod.Should().Be(PaymentMethod.PayPal);
        order.PaymentStatus.Should().Be(PaymentStatus.Pending);

        var payment = order.Payments.Single();
        payment.Method.Should().Be(PaymentMethod.PayPal);
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.GatewayTransactionRef.Should().NotBeNullOrWhiteSpace();

        // Cart is preserved until the payment is captured.
        (await _dbHelper.GetCartItemsAsync(UserA)).Should().ContainSingle();

        // Stock is deducted on create (deduct-on-create policy).
        var stored = await _dbHelper.GetVariantBySkuAsync(variant.Sku);
        stored!.StockQuantity.Should().Be(8);
    }

    [Fact]
    public async Task CompletePayPal_Success_MarksPaidAndClearsCart()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("pp-complete", price: 25.00m, stock: 10);
        await AddToCartAsync(UserA, variant.Id, 1);

        var (orderNumber, token) = await PlacePayPalOrderAsync(UserA);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var completed = await orders.CompletePayPalPaymentAsync(UserA, token);

        completed.Success.Should().BeTrue();
        completed.Data!.OrderNumber.Should().Be(orderNumber);

        var order = await _dbHelper.GetOrderByNumberAsync(orderNumber);
        order!.Status.Should().Be(OrderStatus.Paid);
        order.PaymentStatus.Should().Be(PaymentStatus.Completed);

        var payment = order.Payments.Single();
        payment.Status.Should().Be(PaymentStatus.Completed);
        payment.CompletedAt.Should().NotBeNull();
        payment.RawPayloadJson.Should().NotBeNullOrWhiteSpace();

        (await _dbHelper.GetCartItemsAsync(UserA)).Should().BeEmpty();
    }

    [Fact]
    public async Task CompletePayPal_CalledTwice_IsIdempotent()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("pp-idem", price: 30.00m, stock: 10);
        await AddToCartAsync(UserA, variant.Id, 1);

        var (orderNumber, token) = await PlacePayPalOrderAsync(UserA);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var first = await orders.CompletePayPalPaymentAsync(UserA, token);
        var second = await orders.CompletePayPalPaymentAsync(UserA, token);

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();

        var order = await _dbHelper.GetOrderByNumberAsync(orderNumber);
        order!.PaymentStatus.Should().Be(PaymentStatus.Completed);
        order.Payments.Should().ContainSingle();
        order.Payments.Single().Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public async Task CompletePayPal_CaptureFailure_MarksFailedAndKeepsOrderPending()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("pp-fail", price: 40.00m, stock: 10);
        await AddToCartAsync(UserA, variant.Id, 1);

        var (orderNumber, token) = await PlacePayPalOrderAsync(UserA);

        _factory.PayPalGateway.ForceCaptureFailure = true;

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var completed = await orders.CompletePayPalPaymentAsync(UserA, token);

        completed.Success.Should().BeFalse();

        var order = await _dbHelper.GetOrderByNumberAsync(orderNumber);
        order!.Status.Should().Be(OrderStatus.Pending);
        order.PaymentStatus.Should().Be(PaymentStatus.Failed);
        order.Payments.Single().Status.Should().Be(PaymentStatus.Failed);

        (await _dbHelper.GetCartItemsAsync(UserA)).Should().ContainSingle();
    }

    [Fact]
    public async Task CompletePayPal_AmountMismatch_TreatedAsFailure()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("pp-mismatch", price: 50.00m, stock: 10);
        await AddToCartAsync(UserA, variant.Id, 1);

        var (orderNumber, token) = await PlacePayPalOrderAsync(UserA);

        _factory.PayPalGateway.ForceAmountMismatch = true;

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var completed = await orders.CompletePayPalPaymentAsync(UserA, token);

        completed.Success.Should().BeFalse();

        var order = await _dbHelper.GetOrderByNumberAsync(orderNumber);
        order!.Status.Should().Be(OrderStatus.Pending);
        order.PaymentStatus.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public async Task CancelPayPal_MarksPaymentFailedAndOrderStaysPending()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("pp-cancel", price: 35.00m, stock: 10);
        await AddToCartAsync(UserA, variant.Id, 1);

        var (orderNumber, token) = await PlacePayPalOrderAsync(UserA);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var cancelled = await orders.MarkPayPalCancelledAsync(UserA, token);

        cancelled.Success.Should().BeTrue();

        var order = await _dbHelper.GetOrderByNumberAsync(orderNumber);
        order!.Status.Should().Be(OrderStatus.Pending);
        order.PaymentStatus.Should().Be(PaymentStatus.Failed);
        order.Payments.Single().Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public async Task CompletePayPal_ForAnotherUsersOrder_Fails()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        await _dbHelper.EnsureUserAsync(UserB);
        var variant = await SeedVariantAsync("pp-owner", price: 45.00m, stock: 10);
        await AddToCartAsync(UserA, variant.Id, 1);

        var (orderNumber, token) = await PlacePayPalOrderAsync(UserA);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var completed = await orders.CompletePayPalPaymentAsync(UserB, token);

        completed.Success.Should().BeFalse();

        var order = await _dbHelper.GetOrderByNumberAsync(orderNumber);
        order!.PaymentStatus.Should().Be(PaymentStatus.Pending);
    }

    private async Task<(string OrderNumber, string Token)> PlacePayPalOrderAsync(string userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var result = await orders.CreateOrderFromCartAsync(userId, PayPalCheckout());
        result.Success.Should().BeTrue();

        var order = await _dbHelper.GetOrderByNumberAsync(result.Data!.OrderNumber);
        var token = order!.Payments.Single().GatewayTransactionRef!;
        return (result.Data.OrderNumber, token);
    }

    private static CheckoutRequest PayPalCheckout() => new()
    {
        ShipFullName = "Jane Buyer",
        ShipPhone = "+1-555-0100",
        ShipStreet = "123 Market St",
        ShipCity = "Springfield",
        ShipState = "IL",
        ShipPostalCode = "62701",
        ShipCountry = "USA",
        Method = PaymentMethod.PayPal,
        ReturnUrlBase = ReturnBase
    };

    private async Task AddToCartAsync(string userId, int variantId, int quantity)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var cart = scope.ServiceProvider.GetRequiredService<ICartService>();
        var result = await cart.AddItemAsync(userId, new AddToCartRequest(variantId, quantity));
        result.Success.Should().BeTrue();
    }

    private async Task<ProductVariantDto> SeedVariantAsync(string slug, decimal price, int stock)
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug($"PayPal Cat {slug}", $"paypal-cat-{slug}"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

        var created = await productService.CreateAsync(new CreateProductRequest
        {
            Name = $"PayPal Product {slug}",
            Slug = $"paypal-product-{slug}",
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
