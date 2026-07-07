namespace Nexus.Services.Payments.Models;

/// <summary>
/// Result of capturing a previously created gateway payment.
/// </summary>
public sealed record PaymentResult(
    bool Success,
    string? TransactionRef,
    decimal CapturedAmount,
    string Currency,
    string? RawJson,
    string? Error);
