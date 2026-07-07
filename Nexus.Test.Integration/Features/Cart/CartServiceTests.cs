using Microsoft.Extensions.DependencyInjection;
using Nexus.Services.Cart;
using Nexus.Services.Cart.Models;
using Nexus.Services.Products;
using Nexus.Services.Products.Models;
using Nexus.Test.Integration.TestData;

namespace Nexus.Test.Integration.Features.Cart;

public sealed class CartServiceTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private const string UserA = "cart-user-a-00000000000000001";
    private const string UserB = "cart-user-b-00000000000000002";

    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;
    private readonly DbHelper _dbHelper;

    public CartServiceTests(TestDatabaseFixture fixture)
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
    public async Task AddItemAsync_AddsNewLineToCart()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("add-new", price: 1_000_000m, stock: 10);

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICartService>();

        var result = await service.AddItemAsync(UserA, new AddToCartRequest(variant.Id, 2));

        result.Success.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle();
        result.Data.TotalQuantity.Should().Be(2);

        var stored = await _dbHelper.GetCartItemsAsync(UserA);
        stored.Should().ContainSingle(ci => ci.ProductVariantId == variant.Id && ci.Quantity == 2);
    }

    [Fact]
    public async Task AddItemAsync_ExistingLine_IncrementsQuantity()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("add-existing", price: 500_000m, stock: 10);

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICartService>();

        await service.AddItemAsync(UserA, new AddToCartRequest(variant.Id, 2));
        var result = await service.AddItemAsync(UserA, new AddToCartRequest(variant.Id, 3));

        result.Success.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle();
        result.Data.TotalQuantity.Should().Be(5);
    }

    [Fact]
    public async Task AddItemAsync_ExceedingStock_Fails()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("add-over-stock", price: 100_000m, stock: 3);

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICartService>();

        var result = await service.AddItemAsync(UserA, new AddToCartRequest(variant.Id, 5));

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("in stock");
        (await _dbHelper.GetCartItemsAsync(UserA)).Should().BeEmpty();
    }

    [Fact]
    public async Task AddItemAsync_IncrementBeyondStock_Fails()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("add-increment-over", price: 100_000m, stock: 4);

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICartService>();

        await service.AddItemAsync(UserA, new AddToCartRequest(variant.Id, 3));
        var result = await service.AddItemAsync(UserA, new AddToCartRequest(variant.Id, 3));

        result.Success.Should().BeFalse();
        var stored = await _dbHelper.GetCartItemsAsync(UserA);
        stored.Should().ContainSingle(ci => ci.Quantity == 3);
    }

    [Fact]
    public async Task UpdateItemQuantityAsync_UpdatesQuantity()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("update-qty", price: 100_000m, stock: 10);

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICartService>();

        var added = await service.AddItemAsync(UserA, new AddToCartRequest(variant.Id, 1));
        var cartItemId = added.Data!.Items[0].CartItemId;

        var result = await service.UpdateItemQuantityAsync(UserA, cartItemId, 4);

        result.Success.Should().BeTrue();
        result.Data!.Items[0].Quantity.Should().Be(4);
    }

    [Fact]
    public async Task UpdateItemQuantityAsync_ZeroRemovesLine()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("update-zero", price: 100_000m, stock: 10);

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICartService>();

        var added = await service.AddItemAsync(UserA, new AddToCartRequest(variant.Id, 2));
        var cartItemId = added.Data!.Items[0].CartItemId;

        var result = await service.UpdateItemQuantityAsync(UserA, cartItemId, 0);

        result.Success.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
        (await _dbHelper.GetCartItemsAsync(UserA)).Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateItemQuantityAsync_ExceedingStock_Fails()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("update-over-stock", price: 100_000m, stock: 3);

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICartService>();

        var added = await service.AddItemAsync(UserA, new AddToCartRequest(variant.Id, 1));
        var cartItemId = added.Data!.Items[0].CartItemId;

        var result = await service.UpdateItemQuantityAsync(UserA, cartItemId, 10);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("in stock");
    }

    [Fact]
    public async Task RemoveItemAsync_RemovesLine()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("remove-line", price: 100_000m, stock: 10);

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICartService>();

        var added = await service.AddItemAsync(UserA, new AddToCartRequest(variant.Id, 2));
        var cartItemId = added.Data!.Items[0].CartItemId;

        var result = await service.RemoveItemAsync(UserA, cartItemId);

        result.Success.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearAsync_RemovesAllItems()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant1 = await SeedVariantAsync("clear-1", price: 100_000m, stock: 10);
        var variant2 = await SeedVariantAsync("clear-2", price: 200_000m, stock: 10);

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICartService>();

        await service.AddItemAsync(UserA, new AddToCartRequest(variant1.Id, 1));
        await service.AddItemAsync(UserA, new AddToCartRequest(variant2.Id, 1));

        var result = await service.ClearAsync(UserA);

        result.Success.Should().BeTrue();
        (await _dbHelper.GetCartItemsAsync(UserA)).Should().BeEmpty();
    }

    [Fact]
    public async Task Cart_IsIsolatedPerUser()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        await _dbHelper.EnsureUserAsync(UserB);
        var variant = await SeedVariantAsync("isolation", price: 100_000m, stock: 20);

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICartService>();

        await service.AddItemAsync(UserA, new AddToCartRequest(variant.Id, 2));
        await service.AddItemAsync(UserB, new AddToCartRequest(variant.Id, 5));

        var cartA = await service.GetCartAsync(UserA);
        var cartB = await service.GetCartAsync(UserB);

        cartA.TotalQuantity.Should().Be(2);
        cartB.TotalQuantity.Should().Be(5);
    }

    [Fact]
    public async Task GetItemCountAsync_ReturnsTotalQuantity()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant1 = await SeedVariantAsync("count-1", price: 100_000m, stock: 10);
        var variant2 = await SeedVariantAsync("count-2", price: 200_000m, stock: 10);

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICartService>();

        await service.AddItemAsync(UserA, new AddToCartRequest(variant1.Id, 2));
        await service.AddItemAsync(UserA, new AddToCartRequest(variant2.Id, 3));

        var count = await service.GetItemCountAsync(UserA);

        count.Should().Be(5);
    }

    [Fact]
    public async Task GetCartAsync_AppliesShippingFeeAndVatBelowThreshold()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("summary-fee", price: 20.00m, stock: 10);

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICartService>();

        await service.AddItemAsync(UserA, new AddToCartRequest(variant.Id, 2));

        var cart = await service.GetCartAsync(UserA);

        cart.Subtotal.Should().Be(40.00m);
        cart.ShippingFee.Should().Be(CartPricing.StandardShippingFee);
        cart.TaxAmount.Should().Be(3.20m);
        cart.Total.Should().Be(40.00m + CartPricing.StandardShippingFee + 3.20m);
    }

    [Fact]
    public async Task GetCartAsync_FreeShippingAtOrAboveThreshold()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        var variant = await SeedVariantAsync("summary-free", price: 60.00m, stock: 10);

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICartService>();

        await service.AddItemAsync(UserA, new AddToCartRequest(variant.Id, 2));

        var cart = await service.GetCartAsync(UserA);

        cart.Subtotal.Should().Be(120.00m);
        cart.ShippingFee.Should().Be(0m);
        cart.TaxAmount.Should().Be(9.60m);
        cart.Total.Should().Be(129.60m);
    }

    [Fact]
    public async Task GetCartAsync_EmptyCart_ReturnsZeroedSummary()
    {
        await _dbHelper.EnsureUserAsync(UserA);

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICartService>();

        var cart = await service.GetCartAsync(UserA);

        cart.IsEmpty.Should().BeTrue();
        cart.Subtotal.Should().Be(0m);
        cart.ShippingFee.Should().Be(0m);
        cart.TaxAmount.Should().Be(0m);
        cart.Total.Should().Be(0m);
    }

    private async Task<ProductVariantDto> SeedVariantAsync(string slug, decimal price, int stock)
    {
        var category = await _dbHelper.InsertCategoryAsync(
            TestDataBuilders.ValidCategoryWithSlug($"Cart Cat {slug}", $"cart-cat-{slug}"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

        var created = await productService.CreateAsync(new CreateProductRequest
        {
            Name = $"Cart Product {slug}",
            Slug = $"cart-product-{slug}",
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
