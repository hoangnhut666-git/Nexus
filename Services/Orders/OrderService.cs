using Microsoft.EntityFrameworkCore;
using Nexus.Data;
using Nexus.Data.Entities;
using Nexus.Services.Cart;
using Nexus.Services.Categories.Models;
using Nexus.Services.Orders.Models;
using Nexus.Services.Payments;

namespace Nexus.Services.Orders;

public sealed class OrderService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ICartService cartService,
    IPaymentGatewayResolver gatewayResolver) : IOrderService
{
    public async Task<ServiceResult<PlaceOrderResult>> CreateOrderFromCartAsync(
        string userId,
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult<PlaceOrderResult>.Fail("You must be signed in to place an order.");

        if (request.Method is not (PaymentMethod.Cod or PaymentMethod.PayPal))
            return ServiceResult<PlaceOrderResult>.Fail("Unsupported payment method.");

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

        var payment = new Payment
        {
            Method = request.Method,
            Status = PaymentStatus.Pending,
            Amount = cart.Total,
            Currency = "USD",
            CreatedAt = now
        };

        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.Pending,
            PaymentMethod = request.Method,
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
            Payments = [payment]
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync(cancellationToken);

        order.OrderNumber = OrderNumberGenerator.Generate(order.CreatedAt, order.Id);
        await context.SaveChangesAsync(cancellationToken);

        // PayPal requires buyer approval before capture, so we create the gateway order now
        // and hand back an approval URL. COD settles on delivery with no gateway interaction.
        string? redirectUrl = null;
        if (request.Method == PaymentMethod.PayPal)
        {
            var baseUrl = (request.ReturnUrlBase ?? string.Empty).TrimEnd('/');
            var returnUrl = $"{baseUrl}/checkout/paypal/return";
            var cancelUrl = $"{baseUrl}/checkout/paypal/cancel";

            var gateway = gatewayResolver.Resolve(PaymentMethod.PayPal);
            var initiation = await gateway.CreatePaymentAsync(order, returnUrl, cancelUrl, cancellationToken);

            if (!initiation.Success || initiation.Data?.ApprovalUrl is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ServiceResult<PlaceOrderResult>.Fail(
                    initiation.Error ?? "Could not start the PayPal payment.");
            }

            payment.GatewayTransactionRef = initiation.Data.GatewayOrderId;
            await context.SaveChangesAsync(cancellationToken);
            redirectUrl = initiation.Data.ApprovalUrl;
        }
        else
        {
            // COD: settle the cart immediately.
            await context.CartItems
                .Where(ci => ci.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<PlaceOrderResult>.Ok(new PlaceOrderResult(order.OrderNumber, redirectUrl));
    }

    public async Task<ServiceResult<OrderDto>> CompletePayPalPaymentAsync(
        string userId,
        string paypalOrderId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(paypalOrderId))
            return ServiceResult<OrderDto>.Fail("Order not found.");

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var order = await context.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(
                o => o.UserId == userId
                     && o.Payments.Any(p => p.GatewayTransactionRef == paypalOrderId),
                cancellationToken);

        if (order is null)
            return ServiceResult<OrderDto>.Fail("Order not found.");

        // Idempotency: a refresh or duplicate return must not capture again.
        if (order.PaymentStatus == PaymentStatus.Completed)
            return ServiceResult<OrderDto>.Ok(MapOrder(order));

        if (order.Status == OrderStatus.Cancelled)
            return ServiceResult<OrderDto>.Fail("This order has been cancelled.");

        var payment = order.Payments.First(p => p.GatewayTransactionRef == paypalOrderId);

        var gateway = gatewayResolver.Resolve(PaymentMethod.PayPal);
        var capture = await gateway.CapturePaymentAsync(paypalOrderId, cancellationToken);

        var now = DateTime.UtcNow;

        if (!capture.Success || capture.Data is null || !capture.Data.Success)
        {
            payment.Status = PaymentStatus.Failed;
            order.PaymentStatus = PaymentStatus.Failed;
            order.UpdatedAt = now;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ServiceResult<OrderDto>.Fail(capture.Error ?? capture.Data?.Error ?? "PayPal payment failed.");
        }

        var result = capture.Data;

        if (result.CapturedAmount != order.Total
            || !string.Equals(result.Currency, order.Currency, StringComparison.OrdinalIgnoreCase))
        {
            payment.Status = PaymentStatus.Failed;
            order.PaymentStatus = PaymentStatus.Failed;
            order.UpdatedAt = now;
            payment.RawPayloadJson = result.RawJson;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ServiceResult<OrderDto>.Fail("Captured amount did not match the order total.");
        }

        order.Status = OrderStatus.Paid;
        order.PaymentStatus = PaymentStatus.Completed;
        order.UpdatedAt = now;
        payment.Status = PaymentStatus.Completed;
        payment.CompletedAt = now;
        payment.RawPayloadJson = result.RawJson;

        await context.CartItems
            .Where(ci => ci.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<OrderDto>.Ok(MapOrder(order));
    }

    public async Task<ServiceResult<bool>> MarkPayPalCancelledAsync(
        string userId,
        string paypalOrderId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(paypalOrderId))
            return ServiceResult<bool>.Fail("Order not found.");

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var order = await context.Orders
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(
                o => o.UserId == userId
                     && o.Payments.Any(p => p.GatewayTransactionRef == paypalOrderId),
                cancellationToken);

        if (order is null)
            return ServiceResult<bool>.Fail("Order not found.");

        // A completed payment cannot be cancelled from the return flow.
        if (order.PaymentStatus == PaymentStatus.Completed)
            return ServiceResult<bool>.Ok(true);

        var payment = order.Payments.First(p => p.GatewayTransactionRef == paypalOrderId);
        payment.Status = PaymentStatus.Failed;
        order.PaymentStatus = PaymentStatus.Failed;
        order.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
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
