namespace Nexus.Services.Payments.Models;

/// <summary>
/// Result of starting a payment. For gateways that require redirection (PayPal),
/// <see cref="ApprovalUrl"/> is set and the caller should redirect the user there.
/// For immediate methods (COD) both values are null.
/// </summary>
public sealed record PaymentInitiation(string? GatewayOrderId, string? ApprovalUrl);
