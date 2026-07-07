using Microsoft.Extensions.DependencyInjection;
using Nexus.Services.Cart;
using Nexus.Services.Cart.Models;
using Nexus.Services.Orders;
using Nexus.Services.Orders.Models;
using Nexus.Services.Products;
using Nexus.Services.Products.Models;
using Nexus.Test.Integration.TestData;

namespace Nexus.Test.Integration.Features.Orders;

public sealed class AdminOrderServiceTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private const string UserA = "admin-order-user-a-00000000001";
    private const string UserB = "admin-order-user-b-00000000002";
    private const string AdminId = "admin-actor-000000000000000001";

    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;
    private readonly DbHelper _dbHelper;

    public AdminOrderServiceTests(TestDatabaseFixture fixture)
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
    public async Task GetPagedAsync_FiltersByStatus()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("status-filter", price: 20.00m, stock: 50);

        var pendingId = await PlaceOrderAsync(UserA, variant.Id, 1);
        var shippedId = await PlaceOrderAsync(UserA, variant.Id, 1);
        await _dbHelper.SetOrderStatusAsync(shippedId, OrderStatus.Shipped);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var result = await orders.GetPagedAsync(new AdminOrderQuery { Status = OrderStatus.Shipped });

        result.Items.Should().ContainSingle();
        result.Items[0].Id.Should().Be(shippedId);
        result.Items[0].Status.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByDateRange()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("date-filter", price: 20.00m, stock: 50);

        var oldId = await PlaceOrderAsync(UserA, variant.Id, 1);
        await _dbHelper.SetOrderCreatedAtAsync(oldId, DateTime.UtcNow.AddDays(-30));
        var recentId = await PlaceOrderAsync(UserA, variant.Id, 1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var result = await orders.GetPagedAsync(new AdminOrderQuery
        {
            FromDate = DateTime.UtcNow.AddDays(-2),
            ToDate = DateTime.UtcNow
        });

        result.Items.Should().ContainSingle();
        result.Items[0].Id.Should().Be(recentId);
    }

    [Fact]
    public async Task GetPagedAsync_SearchesByOrderNumberAndCustomer()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        await _dbHelper.EnsureUserAsync(UserB);
        var variant = await SeedVariantAsync("search", price: 20.00m, stock: 50);

        var aId = await PlaceOrderAsync(UserA, variant.Id, 1);
        await PlaceOrderAsync(UserB, variant.Id, 1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        // Customer full name is the userId (see DbHelper.EnsureUserAsync).
        var byCustomer = await orders.GetPagedAsync(new AdminOrderQuery { Search = UserA });
        byCustomer.Items.Should().ContainSingle();
        byCustomer.Items[0].Id.Should().Be(aId);

        var order = await _dbHelper.GetOrderByIdAsync(aId);
        var byNumber = await orders.GetPagedAsync(new AdminOrderQuery { Search = order!.OrderNumber });
        byNumber.Items.Should().ContainSingle();
        byNumber.Items[0].Id.Should().Be(aId);
    }

    [Fact]
    public async Task UpdateStatusAsync_ValidTransition_UpdatesAndRecordsEvent()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("advance", price: 20.00m, stock: 50);
        var orderId = await PlaceOrderAsync(UserA, variant.Id, 1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var result = await orders.UpdateStatusAsync(orderId, OrderStatus.Paid, AdminId, "confirmed");

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be(OrderStatus.Paid);

        var stored = await _dbHelper.GetOrderByIdAsync(orderId);
        stored!.Status.Should().Be(OrderStatus.Paid);

        var events = await _dbHelper.GetOrderEventsAsync(orderId);
        events.Should().ContainSingle();
        events[0].Type.Should().Be(OrderEventType.StatusChanged);
        events[0].OldStatus.Should().Be(OrderStatus.Pending);
        events[0].NewStatus.Should().Be(OrderStatus.Paid);
        events[0].Message.Should().Be("confirmed");
        events[0].CreatedByUserId.Should().Be(AdminId);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidTransition_Rejected()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("invalid-transition", price: 20.00m, stock: 50);
        var orderId = await PlaceOrderAsync(UserA, variant.Id, 1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var result = await orders.UpdateStatusAsync(orderId, OrderStatus.Shipped, AdminId, null);

        result.Success.Should().BeFalse();

        var stored = await _dbHelper.GetOrderByIdAsync(orderId);
        stored!.Status.Should().Be(OrderStatus.Pending);
        (await _dbHelper.GetOrderEventsAsync(orderId)).Should().BeEmpty();
    }

    [Fact]
    public async Task CancelAsync_FromPending_RestoresStockOnceAndRecordsEvent()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("cancel-restore", price: 20.00m, stock: 10);
        var orderId = await PlaceOrderAsync(UserA, variant.Id, 3);

        var afterOrder = await _dbHelper.GetVariantBySkuAsync(variant.Sku);
        afterOrder!.StockQuantity.Should().Be(7);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var result = await orders.CancelAsync(orderId, AdminId, "customer request");

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be(OrderStatus.Cancelled);

        var stored = await _dbHelper.GetOrderByIdAsync(orderId);
        stored!.Status.Should().Be(OrderStatus.Cancelled);
        stored.PaymentStatus.Should().Be(PaymentStatus.Failed);

        var restored = await _dbHelper.GetVariantBySkuAsync(variant.Sku);
        restored!.StockQuantity.Should().Be(10);

        var events = await _dbHelper.GetOrderEventsAsync(orderId);
        events.Should().ContainSingle();
        events[0].NewStatus.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task CancelAsync_AlreadyCancelled_FailsWithoutDoubleRestore()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("double-cancel", price: 20.00m, stock: 10);
        var orderId = await PlaceOrderAsync(UserA, variant.Id, 2);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var first = await orders.CancelAsync(orderId, AdminId, null);
        first.Success.Should().BeTrue();

        var second = await orders.CancelAsync(orderId, AdminId, null);
        second.Success.Should().BeFalse();

        var restored = await _dbHelper.GetVariantBySkuAsync(variant.Sku);
        restored!.StockQuantity.Should().Be(10);
    }

    [Fact]
    public async Task CancelAsync_FromShipped_Rejected()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("cancel-shipped", price: 20.00m, stock: 10);
        var orderId = await PlaceOrderAsync(UserA, variant.Id, 2);
        await _dbHelper.SetOrderStatusAsync(orderId, OrderStatus.Shipped);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var result = await orders.CancelAsync(orderId, AdminId, null);

        result.Success.Should().BeFalse();

        var stored = await _dbHelper.GetOrderByIdAsync(orderId);
        stored!.Status.Should().Be(OrderStatus.Shipped);

        var restored = await _dbHelper.GetVariantBySkuAsync(variant.Sku);
        restored!.StockQuantity.Should().Be(8);
    }

    [Fact]
    public async Task AddNoteAsync_RecordsNoteEvent()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("note", price: 20.00m, stock: 10);
        var orderId = await PlaceOrderAsync(UserA, variant.Id, 1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var result = await orders.AddNoteAsync(orderId, AdminId, "Called customer to confirm address");

        result.Success.Should().BeTrue();

        var events = await _dbHelper.GetOrderEventsAsync(orderId);
        events.Should().ContainSingle();
        events[0].Type.Should().Be(OrderEventType.NoteAdded);
        events[0].Message.Should().Be("Called customer to confirm address");
        events[0].NewStatus.Should().BeNull();
    }

    [Fact]
    public async Task GetByNumberAsync_ReturnsDetailWithEventsAndCustomer()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("detail", price: 20.00m, stock: 10);
        var orderId = await PlaceOrderAsync(UserA, variant.Id, 2);

        await using var scope = _factory.Services.CreateAsyncScope();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();

        await orders.AddNoteAsync(orderId, AdminId, "note one");
        var stored = await _dbHelper.GetOrderByIdAsync(orderId);

        var detail = await orders.GetByNumberAsync(stored!.OrderNumber);

        detail.Should().NotBeNull();
        detail!.Id.Should().Be(orderId);
        detail.CustomerEmail.Should().Be($"{UserA}@integration.test");
        detail.Items.Should().ContainSingle();
        detail.Payments.Should().ContainSingle();
        detail.Events.Should().ContainSingle();
    }

    private async Task<int> PlaceOrderAsync(string userId, int variantId, int quantity)
    {
        await AddToCartAsync(userId, variantId, quantity);

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
            Method = PaymentMethod.Cod
        });

        result.Success.Should().BeTrue();
        var order = await _dbHelper.GetOrderByNumberAsync(result.Data!.OrderNumber);
        return order!.Id;
    }

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
            TestDataBuilders.ValidCategoryWithSlug($"Admin Order Cat {slug}", $"admin-order-cat-{slug}"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

        var created = await productService.CreateAsync(new CreateProductRequest
        {
            Name = $"Admin Order Product {slug}",
            Slug = $"admin-order-product-{slug}",
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
