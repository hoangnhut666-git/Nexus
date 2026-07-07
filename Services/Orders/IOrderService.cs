using Nexus.Services.Categories.Models;
using Nexus.Services.Orders.Models;

namespace Nexus.Services.Orders;

public interface IOrderService
{
    Task<ServiceResult<PlaceOrderResult>> CreateOrderFromCartAsync(
        string userId,
        CheckoutRequest request,
        CancellationToken cancellationToken = default);

    Task<OrderDto?> GetOrderAsync(
        string userId,
        string orderNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderSummaryDto>> GetOrderHistoryAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OrderDto>> CompletePayPalPaymentAsync(
        string userId,
        string paypalOrderId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> MarkPayPalCancelledAsync(
        string userId,
        string paypalOrderId,
        CancellationToken cancellationToken = default);
}
