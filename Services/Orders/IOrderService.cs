using Nexus.Data.Entities;
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

    Task<PagedResult<AdminOrderListItemDto>> GetPagedAsync(
        AdminOrderQuery query,
        CancellationToken cancellationToken = default);

    Task<AdminOrderDetailDto?> GetByNumberAsync(
        string orderNumber,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OrderDto>> UpdateStatusAsync(
        int orderId,
        OrderStatus next,
        string adminUserId,
        string? note,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<OrderDto>> CancelAsync(
        int orderId,
        string adminUserId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> AddNoteAsync(
        int orderId,
        string adminUserId,
        string note,
        CancellationToken cancellationToken = default);
}
