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

    /// <summary>
    /// Absolute base URL of the app (e.g. NavigationManager.BaseUri), used to build
    /// PayPal return/cancel URLs. Ignored for COD.
    /// </summary>
    public string? ReturnUrlBase { get; init; }
}
