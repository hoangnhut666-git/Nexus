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

    public async Task<PagedResult<AdminOrderListItemDto>> GetPagedAsync(
        AdminOrderQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 10 : query.PageSize;

        var orders = context.Orders.AsNoTracking();

        if (query.Status.HasValue)
            orders = orders.Where(o => o.Status == query.Status.Value);

        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value.Date;
            orders = orders.Where(o => o.CreatedAt >= from);
        }

        if (query.ToDate.HasValue)
        {
            var toExclusive = query.ToDate.Value.Date.AddDays(1);
            orders = orders.Where(o => o.CreatedAt < toExclusive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            orders = orders.Where(o =>
                o.OrderNumber.Contains(search)
                || o.ShipFullName.Contains(search)
                || context.Users
                    .Where(u => u.Id == o.UserId)
                    .Any(u => (u.FullName != null && u.FullName.Contains(search))
                              || (u.Email != null && u.Email.Contains(search))));
        }

        var totalCount = await orders.CountAsync(cancellationToken);

        var items = await orders
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new AdminOrderListItemDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                CreatedAt = o.CreatedAt,
                CustomerName = context.Users
                    .Where(u => u.Id == o.UserId)
                    .Select(u => u.FullName)
                    .FirstOrDefault() ?? string.Empty,
                CustomerEmail = context.Users
                    .Where(u => u.Id == o.UserId)
                    .Select(u => u.Email)
                    .FirstOrDefault() ?? string.Empty,
                Status = o.Status,
                PaymentMethod = o.PaymentMethod,
                PaymentStatus = o.PaymentStatus,
                Total = o.Total,
                ItemCount = o.Items.Sum(i => i.Quantity)
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminOrderListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminOrderDetailDto?> GetByNumberAsync(
        string orderNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
            return null;

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var order = await context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.Events)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, cancellationToken);

        if (order is null)
            return null;

        var customer = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == order.UserId)
            .Select(u => new { u.FullName, u.Email })
            .FirstOrDefaultAsync(cancellationToken);

        return new AdminOrderDetailDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status,
            PaymentMethod = order.PaymentMethod,
            PaymentStatus = order.PaymentStatus,
            Subtotal = order.Subtotal,
            ShippingFee = order.ShippingFee,
            TaxAmount = order.TaxAmount,
            Total = order.Total,
            Currency = order.Currency,
            CustomerName = customer?.FullName ?? string.Empty,
            CustomerEmail = customer?.Email ?? string.Empty,
            ShipFullName = order.ShipFullName,
            ShipPhone = order.ShipPhone,
            ShipStreet = order.ShipStreet,
            ShipCity = order.ShipCity,
            ShipState = order.ShipState,
            ShipPostalCode = order.ShipPostalCode,
            ShipCountry = order.ShipCountry,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
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
                .ToList(),
            Payments = order.Payments
                .OrderBy(p => p.Id)
                .Select(p => new PaymentDto
                {
                    Method = p.Method,
                    Status = p.Status,
                    Amount = p.Amount,
                    Currency = p.Currency,
                    GatewayTransactionRef = p.GatewayTransactionRef,
                    CreatedAt = p.CreatedAt,
                    CompletedAt = p.CompletedAt
                })
                .ToList(),
            Events = order.Events
                .OrderByDescending(e => e.CreatedAt)
                .ThenByDescending(e => e.Id)
                .Select(e => new OrderEventDto
                {
                    Type = e.Type,
                    OldStatus = e.OldStatus,
                    NewStatus = e.NewStatus,
                    Message = e.Message,
                    CreatedAt = e.CreatedAt,
                    CreatedByUserId = e.CreatedByUserId
                })
                .ToList()
        };
    }

    public async Task<ServiceResult<OrderDto>> UpdateStatusAsync(
        int orderId,
        OrderStatus next,
        string adminUserId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
            return ServiceResult<OrderDto>.Fail("Missing administrator identity.");

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var order = await context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
            return ServiceResult<OrderDto>.Fail("Order not found.");

        if (!OrderStatusRules.AllowedNext(order.Status).Contains(next))
            return ServiceResult<OrderDto>.Fail(
                $"Cannot move an order from {order.Status} to {next}.");

        var previous = order.Status;
        var now = DateTime.UtcNow;

        order.Status = next;
        order.UpdatedAt = now;

        context.OrderEvents.Add(new OrderEvent
        {
            OrderId = order.Id,
            Type = OrderEventType.StatusChanged,
            OldStatus = previous,
            NewStatus = next,
            Message = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedByUserId = adminUserId,
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<OrderDto>.Ok(MapOrder(order));
    }

    public async Task<ServiceResult<OrderDto>> CancelAsync(
        int orderId,
        string adminUserId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
            return ServiceResult<OrderDto>.Fail("Missing administrator identity.");

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var order = await context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
            return ServiceResult<OrderDto>.Fail("Order not found.");

        if (order.Status == OrderStatus.Cancelled)
            return ServiceResult<OrderDto>.Fail("This order is already cancelled.");

        if (!OrderStatusRules.CanCancel(order.Status))
            return ServiceResult<OrderDto>.Fail(
                $"An order in {order.Status} status cannot be cancelled.");

        var previous = order.Status;
        var now = DateTime.UtcNow;

        // Restore the stock reserved at order creation (deduct-on-create policy).
        foreach (var item in order.Items)
        {
            await context.ProductVariants
                .Where(v => v.Id == item.ProductVariantId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity + item.Quantity),
                    cancellationToken);
        }

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = now;

        // Online refunds are out of scope; only mark not-yet-settled payments as failed.
        if (order.PaymentStatus == PaymentStatus.Pending)
            order.PaymentStatus = PaymentStatus.Failed;

        context.OrderEvents.Add(new OrderEvent
        {
            OrderId = order.Id,
            Type = OrderEventType.StatusChanged,
            OldStatus = previous,
            NewStatus = OrderStatus.Cancelled,
            Message = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            CreatedByUserId = adminUserId,
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<OrderDto>.Ok(MapOrder(order));
    }

    public async Task<ServiceResult<bool>> AddNoteAsync(
        int orderId,
        string adminUserId,
        string note,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
            return ServiceResult<bool>.Fail("Missing administrator identity.");

        if (string.IsNullOrWhiteSpace(note))
            return ServiceResult<bool>.Fail("Note cannot be empty.");

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var exists = await context.Orders.AnyAsync(o => o.Id == orderId, cancellationToken);
        if (!exists)
            return ServiceResult<bool>.Fail("Order not found.");

        context.OrderEvents.Add(new OrderEvent
        {
            OrderId = orderId,
            Type = OrderEventType.NoteAdded,
            Message = note.Trim(),
            CreatedByUserId = adminUserId,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
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
