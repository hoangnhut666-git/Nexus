using Microsoft.EntityFrameworkCore;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Services.Cart;
using Nexus.Services.Categories.Models;
using Nexus.Services.Orders.Models;

namespace Nexus.Services.Orders;

public sealed class OrderService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ICartService cartService) : IOrderService
{
    public async Task<ServiceResult<PlaceOrderResult>> CreateOrderFromCartAsync(
        string userId,
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult<PlaceOrderResult>.Fail("You must be signed in to place an order.");

        if (request.Method != PaymentMethod.Cod)
            return ServiceResult<PlaceOrderResult>.Fail("Only Cash on Delivery is supported right now.");

        var cart = await cartService.GetCartAsync(userId, cancellationToken);

        if (cart.IsEmpty)
            return ServiceResult<PlaceOrderResult>.Fail("Your cart is empty.");

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var variantIds = cart.Items.Select(l => l.ProductVariantId).ToList();

        var variants = await context.ProductVariants
            .Include(v => v.Product)
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        foreach (var line in cart.Items)
        {
            if (!variants.TryGetValue(line.ProductVariantId, out var variant)
                || !variant.IsActive
                || !variant.Product.IsActive)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ServiceResult<PlaceOrderResult>.Fail(
                    $"'{line.ProductName}' is no longer available.");
            }
        }

        foreach (var line in cart.Items)
        {
            var affected = await context.ProductVariants
                .Where(v => v.Id == line.ProductVariantId && v.StockQuantity >= line.Quantity)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity - line.Quantity),
                    cancellationToken);

            if (affected != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ServiceResult<PlaceOrderResult>.Fail(
                    $"'{line.ProductName}' is out of stock.");
            }
        }

        var now = DateTime.UtcNow;

        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.Pending,
            PaymentMethod = PaymentMethod.Cod,
            PaymentStatus = PaymentStatus.Pending,
            Subtotal = cart.Subtotal,
            ShippingFee = cart.ShippingFee,
            TaxAmount = cart.TaxAmount,
            Total = cart.Total,
            Currency = "USD",
            ShipFullName = request.ShipFullName,
            ShipPhone = request.ShipPhone,
            ShipStreet = request.ShipStreet,
            ShipCity = request.ShipCity,
            ShipState = request.ShipState,
            ShipPostalCode = request.ShipPostalCode,
            ShipCountry = request.ShipCountry,
            CreatedAt = now,
            UpdatedAt = now,
            Items = cart.Items.Select(line => new OrderItem
            {
                ProductVariantId = line.ProductVariantId,
                ProductName = line.ProductName,
                VariantLabel = line.VariantLabel,
                Sku = line.Sku,
                UnitPrice = line.UnitPrice,
                Quantity = line.Quantity,
                LineTotal = line.LineTotal
            }).ToList(),
            Payments =
            [
                new Payment
                {
                    Method = PaymentMethod.Cod,
                    Status = PaymentStatus.Pending,
                    Amount = cart.Total,
                    Currency = "USD",
                    CreatedAt = now
                }
            ]
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync(cancellationToken);

        order.OrderNumber = OrderNumberGenerator.Generate(order.CreatedAt, order.Id);
        await context.SaveChangesAsync(cancellationToken);

        await context.CartItems
            .Where(ci => ci.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PlaceOrderResult>.Ok(new PlaceOrderResult(order.OrderNumber));
    }

    public async Task<OrderDto?> GetOrderAsync(
        string userId,
        string orderNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(orderNumber))
            return null;

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var order = await context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(
                o => o.UserId == userId && o.OrderNumber == orderNumber,
                cancellationToken);

        return order is null ? null : MapOrder(order);
    }

    public async Task<IReadOnlyList<OrderSummaryDto>> GetOrderHistoryAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Orders
            .AsNoTracking()
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Select(o => new OrderSummaryDto
            {
                OrderNumber = o.OrderNumber,
                CreatedAt = o.CreatedAt,
                Status = o.Status,
                Total = o.Total,
                ItemCount = o.Items.Sum(i => i.Quantity)
            })
            .ToListAsync(cancellationToken);
    }

    private static OrderDto MapOrder(Order order) => new()
    {
        OrderNumber = order.OrderNumber,
        Status = order.Status,
        PaymentMethod = order.PaymentMethod,
        PaymentStatus = order.PaymentStatus,
        Subtotal = order.Subtotal,
        ShippingFee = order.ShippingFee,
        TaxAmount = order.TaxAmount,
        Total = order.Total,
        Currency = order.Currency,
        ShipFullName = order.ShipFullName,
        ShipPhone = order.ShipPhone,
        ShipStreet = order.ShipStreet,
        ShipCity = order.ShipCity,
        ShipState = order.ShipState,
        ShipPostalCode = order.ShipPostalCode,
        ShipCountry = order.ShipCountry,
        CreatedAt = order.CreatedAt,
        Items = order.Items
            .OrderBy(i => i.Id)
            .Select(i => new OrderLineDto
            {
                ProductVariantId = i.ProductVariantId,
                ProductName = i.ProductName,
                VariantLabel = i.VariantLabel,
                Sku = i.Sku,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                LineTotal = i.LineTotal
            })
            .ToList()
    };
}
