namespace Nexus.Services.Orders.Models;

public sealed record PlaceOrderResult(string OrderNumber, string? RedirectUrl = null);
