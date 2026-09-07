namespace Nexus.Services.Demo;

public interface IDemoCheckoutCtaService
{
    Task<CheckoutCtaVariant> GetAsync(CancellationToken cancellationToken = default);

    Task SetAsync(CheckoutCtaVariant variant, CancellationToken cancellationToken = default);
}
