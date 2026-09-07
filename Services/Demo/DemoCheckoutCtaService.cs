using Microsoft.EntityFrameworkCore;
using Nexus.Data;
using Nexus.Data.Entities;

namespace Nexus.Services.Demo;

public sealed class DemoCheckoutCtaService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory) : IDemoCheckoutCtaService
{
    public const string SettingKey = "CheckoutCtaVariant";

    public async Task<CheckoutCtaVariant> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var setting = await context.DemoAppSettings
            .FirstOrDefaultAsync(s => s.Key == SettingKey, cancellationToken);

        if (setting is not null
            && Enum.TryParse<CheckoutCtaVariant>(setting.Value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        await UpsertAsync(context, CheckoutCtaVariant.Baseline, cancellationToken);
        return CheckoutCtaVariant.Baseline;
    }

    public async Task SetAsync(CheckoutCtaVariant variant, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(variant))
            variant = CheckoutCtaVariant.Baseline;

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await UpsertAsync(context, variant, cancellationToken);
    }

    private static async Task UpsertAsync(
        ApplicationDbContext context,
        CheckoutCtaVariant variant,
        CancellationToken cancellationToken)
    {
        var setting = await context.DemoAppSettings
            .FirstOrDefaultAsync(s => s.Key == SettingKey, cancellationToken);

        var value = variant.ToString();
        var now = DateTime.UtcNow;

        if (setting is null)
        {
            context.DemoAppSettings.Add(new DemoAppSetting
            {
                Key = SettingKey,
                Value = value,
                UpdatedAt = now
            });
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
