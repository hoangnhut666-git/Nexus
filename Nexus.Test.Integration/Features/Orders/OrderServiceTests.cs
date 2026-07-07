using Microsoft.Extensions.DependencyInjection;
using Nexus.Services.Cart;
using Nexus.Services.Cart.Models;
using Nexus.Services.Orders;
using Nexus.Services.Orders.Models;
using Nexus.Services.Products;
using Nexus.Services.Products.Models;
using Nexus.Test.Integration.TestData;

namespace Nexus.Test.Integration.Features.Orders;

public sealed class OrderServiceTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private const string UserA = "order-user-a-0000000000000001";
    private const string UserB = "order-user-b-0000000000000002";

    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;
    private readonly DbHelper _dbHelper;

    public OrderServiceTests(TestDatabaseFixture fixture)
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
    public async Task CreateOrderFromCartAsync_EmptyCart_Fails()
    {
        await _dbHelper.EnsureUserAsync(UserA);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var result = await orders.CreateOrderFromCartAsync(UserA, ValidCheckout());

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();

        var stored = await _dbHelper.GetOrdersAsync(UserA);
        stored.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_HappyPath_PersistsOrderAndClearsCart()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("happy", price: 20.00m, stock: 10);
        await AddToCartAsync(UserA, variant.Id, 2);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var result = await orders.CreateOrderFromCartAsync(UserA, ValidCheckout());

        result.Success.Should().BeTrue();
        result.Data!.OrderNumber.Should().MatchRegex(@"^NX-\d{8}-\d{6}$");
        result.Data.RedirectUrl.Should().BeNull();

        var order = await _dbHelper.GetOrderByNumberAsync(result.Data.OrderNumber);
        order.Should().NotBeNull();
        order!.UserId.Should().Be(UserA);
        order.Status.Should().Be(OrderStatus.Pending);
        order.PaymentMethod.Should().Be(PaymentMethod.Cod);
        order.PaymentStatus.Should().Be(PaymentStatus.Pending);
        order.Subtotal.Should().Be(40.00m);
        order.ShippingFee.Should().Be(9.99m);
        order.TaxAmount.Should().Be(3.20m);
        order.Total.Should().Be(53.19m);
        order.Currency.Should().Be("USD");

        order.Items.Should().ContainSingle();
        var item = order.Items.Single();
        item.ProductVariantId.Should().Be(variant.Id);
        item.UnitPrice.Should().Be(20.00m);
        item.Quantity.Should().Be(2);
        item.LineTotal.Should().Be(40.00m);

        order.Payments.Should().ContainSingle();
        var payment = order.Payments.Single();
        payment.Method.Should().Be(PaymentMethod.Cod);
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.Amount.Should().Be(53.19m);

        var cart = await _dbHelper.GetCartItemsAsync(UserA);
        cart.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_DeductsStock()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("deduct", price: 15.00m, stock: 10);
        await AddToCartAsync(UserA, variant.Id, 3);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var result = await orders.CreateOrderFromCartAsync(UserA, ValidCheckout());

        result.Success.Should().BeTrue();

        var stored = await _dbHelper.GetVariantBySkuAsync(variant.Sku);
        stored!.StockQuantity.Should().Be(7);
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_StaleOutOfStock_RejectsAndPersistsNothing()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("stale", price: 15.00m, stock: 5);
        await AddToCartAsync(UserA, variant.Id, 3);

        await SetStockAsync(variant.Id, 2);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var result = await orders.CreateOrderFromCartAsync(UserA, ValidCheckout());

        result.Success.Should().BeFalse();

        (await _dbHelper.GetOrdersAsync(UserA)).Should().BeEmpty();

        var stored = await _dbHelper.GetVariantBySkuAsync(variant.Sku);
        stored!.StockQuantity.Should().Be(2);

        (await _dbHelper.GetCartItemsAsync(UserA)).Should().ContainSingle();
    }

    [Fact]
    public async Task CreateOrderFromCartAsync_InactiveVariant_Rejected()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("inactive", price: 15.00m, stock: 10);
        await AddToCartAsync(UserA, variant.Id, 2);

        await SetVariantActiveAsync(variant.Id, isActive: false);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var result = await orders.CreateOrderFromCartAsync(UserA, ValidCheckout());

        result.Success.Should().BeFalse();

        (await _dbHelper.GetOrdersAsync(UserA)).Should().BeEmpty();

        var stored = await _dbHelper.GetVariantBySkuAsync(variant.Sku);
        stored!.StockQuantity.Should().Be(10);
    }

    [Fact]
    public async Task GetOrderAsync_ReturnsOwnOrder_AndNullForOtherUser()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        await _dbHelper.EnsureUserAsync(UserB);
        var variant = await SeedVariantAsync("ownership", price: 30.00m, stock: 10);
        await AddToCartAsync(UserA, variant.Id, 1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var placed = await orders.CreateOrderFromCartAsync(UserA, ValidCheckout());
        placed.Success.Should().BeTrue();
        var orderNumber = placed.Data!.OrderNumber;

        var own = await orders.GetOrderAsync(UserA, orderNumber);
        own.Should().NotBeNull();
        own!.OrderNumber.Should().Be(orderNumber);
        own.Items.Should().ContainSingle();

        var foreignView = await orders.GetOrderAsync(UserB, orderNumber);
        foreignView.Should().BeNull();
    }

    [Fact]
    public async Task GetOrderHistoryAsync_ReturnsNewestFirstScopedToUser()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        await _dbHelper.EnsureUserAsync(UserB);
        var variant = await SeedVariantAsync("history", price: 25.00m, stock: 20);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        await AddToCartAsync(UserA, variant.Id, 1);
        var first = await orders.CreateOrderFromCartAsync(UserA, ValidCheckout());

        await AddToCartAsync(UserA, variant.Id, 2);
        var second = await orders.CreateOrderFromCartAsync(UserA, ValidCheckout());

        await AddToCartAsync(UserB, variant.Id, 1);
        await orders.CreateOrderFromCartAsync(UserB, ValidCheckout());

        var history = await orders.GetOrderHistoryAsync(UserA);

        history.Should().HaveCount(2);
        history[0].OrderNumber.Should().Be(second.Data!.OrderNumber);
        history[1].OrderNumber.Should().Be(first.Data!.OrderNumber);
        history[0].ItemCount.Should().Be(2);
        history[1].ItemCount.Should().Be(1);
    }

    private static CheckoutRequest ValidCheckout() => new()
    {
        ShipFullName = "Jane Buyer",
        ShipPhone = "+1-555-0100",
        ShipStreet = "123 Market St",
        ShipCity = "Springfield",
        ShipState = "IL",
        ShipPostalCode = "62701",
        ShipCountry = "USA",
        Method = PaymentMethod.Cod
    };

    private async Task AddToCartAsync(string userId, int variantId, int quantity)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var cart = scope.ServiceProvider.GetRequiredService<ICartService>();
        var result = await cart.AddItemAsync(userId, new AddToCartRequest(variantId, quantity));
        result.Success.Should().BeTrue();
    }

    private async Task SetStockAsync(int variantId, int stock)
    {
        await using var db = _dbHelper.CreateContext();
        var variant = await db.ProductVariants.FirstAsync(v => v.Id == variantId);
        variant.StockQuantity = stock;
        await db.SaveChangesAsync();
    }

    private async Task SetVariantActiveAsync(int variantId, bool isActive)
    {
        await using var db = _dbHelper.CreateContext();
        var variant = await db.ProductVariants.FirstAsync(v => v.Id == variantId);
        variant.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    private async Task<ProductVariantDto> SeedVariantAsync(string slug, decimal price, int stock)
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug($"Order Cat {slug}", $"order-cat-{slug}"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

        var created = await productService.CreateAsync(new CreateProductRequest
        {
            Name = $"Order Product {slug}",
            Slug = $"order-product-{slug}",
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
