using Nexus.Data.Entities;

namespace Nexus.Services.Payments;

public sealed class PaymentGatewayResolver(IEnumerable<IPaymentGateway> gateways) : IPaymentGatewayResolver
{
    public IPaymentGateway Resolve(PaymentMethod method) =>
        gateways.FirstOrDefault(g => g.Method == method)
        ?? throw new InvalidOperationException($"No payment gateway registered for method '{method}'.");
}
