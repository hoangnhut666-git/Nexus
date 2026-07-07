using Nexus.Data.Entities;

namespace Nexus.Services.Orders.Models;

public sealed class OrderSummaryDto
{
    public string OrderNumber { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public OrderStatus Status { get; init; }

    public decimal Total { get; init; }

    public int ItemCount { get; init; }
}
