namespace Nexus.Services.Demo;

public interface IDemoAddToCartCtaService
{
    Task<AddToCartCtaVariant> GetAsync(CancellationToken cancellationToken = default);

    Task SetAsync(AddToCartCtaVariant variant, CancellationToken cancellationToken = default);
}
