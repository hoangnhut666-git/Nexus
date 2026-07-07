using Nexus.Data.Entities;

namespace Nexus.Services.Orders.Models;

public sealed class OrderDto
{
    public string OrderNumber { get; init; } = string.Empty;

    public OrderStatus Status { get; init; }

    public PaymentMethod PaymentMethod { get; init; }

    public PaymentStatus PaymentStatus { get; init; }

    public decimal Subtotal { get; init; }

    public decimal ShippingFee { get; init; }

    public decimal TaxAmount { get; init; }

    public decimal Total { get; init; }

    public string Currency { get; init; } = "USD";

    public string ShipFullName { get; init; } = string.Empty;

    public string ShipPhone { get; init; } = string.Empty;

    public string ShipStreet { get; init; } = string.Empty;

    public string ShipCity { get; init; } = string.Empty;

    public string? ShipState { get; init; }

    public string? ShipPostalCode { get; init; }

    public string ShipCountry { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public IReadOnlyList<OrderLineDto> Items { get; init; } = [];
}
