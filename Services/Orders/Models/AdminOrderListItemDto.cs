using Nexus.Data.Entities;

namespace Nexus.Services.Orders.Models;

public sealed class AdminOrderListItemDto
{
    public int Id { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public string CustomerEmail { get; init; } = string.Empty;

    public OrderStatus Status { get; init; }

    public PaymentMethod PaymentMethod { get; init; }

    public PaymentStatus PaymentStatus { get; init; }

    public decimal Total { get; init; }

    public int ItemCount { get; init; }
}
