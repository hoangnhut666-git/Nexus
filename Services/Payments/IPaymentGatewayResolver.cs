using Nexus.Data.Entities;

namespace Nexus.Services.Payments;

public interface IPaymentGatewayResolver
{
    IPaymentGateway Resolve(PaymentMethod method);
}
