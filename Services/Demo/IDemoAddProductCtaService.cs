namespace Nexus.Services.Demo;

public interface IDemoAddProductCtaService
{
    Task<AddProductCtaVariant> GetAsync(CancellationToken cancellationToken = default);

    Task SetAsync(AddProductCtaVariant variant, CancellationToken cancellationToken = default);
}
