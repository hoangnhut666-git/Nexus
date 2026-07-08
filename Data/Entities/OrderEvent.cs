namespace Nexus.Data.Entities;

public class OrderEvent
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public OrderEventType Type { get; set; }

    public OrderStatus? OldStatus { get; set; }

    public OrderStatus? NewStatus { get; set; }

    public string? Message { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Order Order { get; set; } = null!;
}
