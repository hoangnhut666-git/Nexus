namespace Nexus.Data.Entities;

public class Order
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public OrderStatus Status { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public PaymentStatus PaymentStatus { get; set; }

    public decimal Subtotal { get; set; }

    public decimal ShippingFee { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal Total { get; set; }

    public string Currency { get; set; } = "USD";

    public string ShipFullName { get; set; } = string.Empty;

    public string ShipPhone { get; set; } = string.Empty;

    public string ShipStreet { get; set; } = string.Empty;

    public string ShipCity { get; set; } = string.Empty;

    public string? ShipState { get; set; }

    public string? ShipPostalCode { get; set; }

    public string ShipCountry { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];

    public ICollection<Payment> Payments { get; set; } = [];

    public ICollection<OrderEvent> Events { get; set; } = [];
}
