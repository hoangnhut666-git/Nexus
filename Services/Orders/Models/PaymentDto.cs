using Nexus.Data.Entities;

namespace Nexus.Services.Orders.Models;

public sealed class PaymentDto
{
    public PaymentMethod Method { get; init; }

    public PaymentStatus Status { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = "USD";

    public string? GatewayTransactionRef { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? CompletedAt { get; init; }
}
