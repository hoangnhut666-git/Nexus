using System.Collections.Concurrent;
using Nexus.Data.Entities;
using Nexus.Services.Categories.Models;
using Nexus.Services.Payments;
using Nexus.Services.Payments.Models;

namespace Nexus.Test.Integration.Infrastructure;

/// <summary>
/// In-memory stand-in for <see cref="PayPalPaymentGateway"/> so integration tests exercise
/// the create/capture flow with no network. Registered as a singleton so a test can flip the
/// controllable flags before driving the OrderService.
/// </summary>
public sealed class FakePayPalGateway : IPaymentGateway
{
    private readonly ConcurrentDictionary<string, decimal> _amounts = new();

    public PaymentMethod Method => PaymentMethod.PayPal;

    /// <summary>When true, <see cref="CapturePaymentAsync"/> reports a failed capture.</summary>
    public bool ForceCaptureFailure { get; set; }

    /// <summary>When true, the captured amount is deliberately off by $1 to test verification.</summary>
    public bool ForceAmountMismatch { get; set; }

    /// <summary>When true, <see cref="CreatePaymentAsync"/> reports a failure (no approval URL).</summary>
    public bool ForceCreateFailure { get; set; }

    public Task<ServiceResult<PaymentInitiation>> CreatePaymentAsync(
        Order order,
        string returnUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        if (ForceCreateFailure)
            return Task.FromResult(ServiceResult<PaymentInitiation>.Fail("Forced create failure."));

        var gatewayOrderId = $"FAKE-{Guid.NewGuid():N}";
        _amounts[gatewayOrderId] = order.Total;

        var approvalUrl = $"{returnUrl}?token={gatewayOrderId}";
        return Task.FromResult(ServiceResult<PaymentInitiation>.Ok(
            new PaymentInitiation(gatewayOrderId, approvalUrl)));
    }

    public Task<ServiceResult<PaymentResult>> CapturePaymentAsync(
        string gatewayOrderId,
        CancellationToken cancellationToken = default)
    {
        if (ForceCaptureFailure)
            return Task.FromResult(ServiceResult<PaymentResult>.Ok(
                new PaymentResult(false, null, 0m, "USD", "{\"status\":\"DECLINED\"}", "Forced capture failure.")));

        var amount = _amounts.TryGetValue(gatewayOrderId, out var recorded) ? recorded : 0m;
        if (ForceAmountMismatch)
            amount += 1m;

        return Task.FromResult(ServiceResult<PaymentResult>.Ok(new PaymentResult(
            Success: true,
            TransactionRef: $"CAPTURE-{gatewayOrderId}",
            CapturedAmount: amount,
            Currency: "USD",
            RawJson: "{\"status\":\"COMPLETED\"}",
            Error: null)));
    }
}
