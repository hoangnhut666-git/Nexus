using Nexus.Data.Entities;

namespace Nexus.Services.Orders;

/// <summary>
/// Central definition of the order status workflow (SRS 6.3). Reused by the service
/// (to guard transitions) and the admin UI (to render only valid controls).
/// Cancellation is handled separately via <c>CancelAsync</c> so it can restore stock.
/// </summary>
public static class OrderStatusRules
{
    public static IReadOnlyList<OrderStatus> AllowedNext(OrderStatus current) => current switch
    {
        OrderStatus.Pending => [OrderStatus.Paid],
        OrderStatus.Paid => [OrderStatus.Processing],
        OrderStatus.Processing => [OrderStatus.Shipped],
        OrderStatus.Shipped => [OrderStatus.Delivered],
        _ => []
    };

    public static bool CanCancel(OrderStatus current) =>
        current is OrderStatus.Pending or OrderStatus.Paid;
}
