using Nexus.Data.Entities;
using Nexus.Services.Categories.Models;
using Nexus.Services.Payments.Models;

namespace Nexus.Services.Payments;

/// <summary>
/// Cash on Delivery has no gateway interaction: there is nothing to redirect to and
/// nothing to capture online. Payment is settled when the courier collects cash.
/// </summary>
public sealed class CodPaymentGateway : IPaymentGateway
{
    public PaymentMethod Method => PaymentMethod.Cod;

    public Task<ServiceResult<PaymentInitiation>> CreatePaymentAsync(
        Order order,
        string returnUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ServiceResult<PaymentInitiation>.Ok(new PaymentInitiation(null, null)));

    public Task<ServiceResult<PaymentResult>> CapturePaymentAsync(
        string gatewayOrderId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ServiceResult<PaymentResult>.Ok(
            new PaymentResult(true, null, 0m, "USD", null, null)));
}
