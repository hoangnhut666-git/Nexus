namespace Nexus.Data.Entities;

public class Payment
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public PaymentMethod Method { get; set; }

    public PaymentStatus Status { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public string? GatewayTransactionRef { get; set; }

    public string? RawPayloadJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public Order Order { get; set; } = null!;
}
