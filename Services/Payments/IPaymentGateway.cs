using Nexus.Data.Entities;
using Nexus.Services.Categories.Models;
using Nexus.Services.Payments.Models;

namespace Nexus.Services.Payments;

/// <summary>
/// Abstraction over a payment method. Implementations create a gateway-side payment
/// (returning an approval URL when redirection is required) and capture it on return.
/// </summary>
public interface IPaymentGateway
{
    PaymentMethod Method { get; }

    Task<ServiceResult<PaymentInitiation>> CreatePaymentAsync(
        Order order,
        string returnUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<PaymentResult>> CapturePaymentAsync(
        string gatewayOrderId,
        CancellationToken cancellationToken = default);
}
