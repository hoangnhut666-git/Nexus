using Microsoft.EntityFrameworkCore;
using Nexus.Data;
using Nexus.Data.Entities;

namespace Nexus.Services.Demo;

public sealed class DemoAddProductCtaService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory) : IDemoAddProductCtaService
{
    public const string SettingKey = "AddProductCtaVariant";

    public async Task<AddProductCtaVariant> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var setting = await context.DemoAppSettings
            .FirstOrDefaultAsync(s => s.Key == SettingKey, cancellationToken);

        if (setting is not null
            && Enum.TryParse<AddProductCtaVariant>(setting.Value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        await UpsertAsync(context, AddProductCtaVariant.Baseline, cancellationToken);
        return AddProductCtaVariant.Baseline;
    }

    public async Task SetAsync(AddProductCtaVariant variant, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(variant))
            variant = AddProductCtaVariant.Baseline;

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await UpsertAsync(context, variant, cancellationToken);
    }

    private static async Task UpsertAsync(
        ApplicationDbContext context,
        AddProductCtaVariant variant,
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
