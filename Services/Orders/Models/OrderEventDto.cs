using Nexus.Data.Entities;

namespace Nexus.Services.Orders.Models;

public sealed class OrderEventDto
{
    public OrderEventType Type { get; init; }

    public OrderStatus? OldStatus { get; init; }

    public OrderStatus? NewStatus { get; init; }

    public string? Message { get; init; }

    public DateTime CreatedAt { get; init; }

    public string CreatedByUserId { get; init; } = string.Empty;
}
