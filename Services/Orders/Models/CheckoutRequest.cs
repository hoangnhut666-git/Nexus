using Nexus.Data.Entities;

namespace Nexus.Services.Orders.Models;

public sealed class CheckoutRequest
{
    public string ShipFullName { get; init; } = string.Empty;

    public string ShipPhone { get; init; } = string.Empty;

    public string ShipStreet { get; init; } = string.Empty;

    public string ShipCity { get; init; } = string.Empty;

    public string? ShipState { get; init; }

    public string? ShipPostalCode { get; init; }

    public string ShipCountry { get; init; } = string.Empty;

    public PaymentMethod Method { get; init; } = PaymentMethod.Cod;
}
