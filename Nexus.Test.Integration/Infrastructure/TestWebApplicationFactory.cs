using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Nexus.Test.Integration.Infrastructure.Auth;

namespace Nexus.Test.Integration.Infrastructure;

/// <summary>
/// Boots the real ASP.NET Core application with targeted overrides for integration testing:
/// SQL Server connection string swap, in-memory Identity settings, and fake test auth.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly IReadOnlyDictionary<string, string?>? _additionalConfig;

    public TestWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public TestWebApplicationFactory(
        string connectionString,
        IReadOnlyDictionary<string, string?> additionalConfig)
    {
        _connectionString = connectionString;
        _additionalConfig = additionalConfig;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["IdentitySettings:AdminPassword"] = "",
                ["IdentitySettings:RequireConfirmedAccount"] = "false",
                ["RunStartupMigrations"] = "false",
            });

            if (_additionalConfig is not null)
                config.AddInMemoryCollection(_additionalConfig);
        });

        builder.ConfigureServices((_, services) =>
        {
            var dbDescriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                || d.ServiceType == typeof(IDbContextFactory<ApplicationDbContext>)
                || d.ServiceType == typeof(ApplicationDbContext)).ToList();

            foreach (var descriptor in dbDescriptors)
                services.Remove(descriptor);

            services.AddDbContextFactory<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(_connectionString);
                options.ConfigureWarnings(w =>
                    w.Ignore(RelationalEventId.PendingModelChangesWarning));
            });

            services.AddScoped<ApplicationDbContext>(sp =>
                sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

            services.RemoveAll<IAuthenticationSchemeProvider>();
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                options.DefaultSignInScheme = TestAuthHandler.SchemeName;
                options.DefaultSignOutScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

            services.AddAuthorization(options =>
            {
                var testPolicy = new AuthorizationPolicyBuilder(TestAuthHandler.SchemeName)
                    .RequireAuthenticatedUser()
                    .Build();

                options.DefaultPolicy = testPolicy;
                options.FallbackPolicy = null;

                options.AddPolicy("Admin", policy => policy
                    .AddAuthenticationSchemes(TestAuthHandler.SchemeName)
                    .RequireAuthenticatedUser()
                    .RequireRole(IdentitySeedData.AdminRole));

                options.AddPolicy("Customer", policy => policy
                    .AddAuthenticationSchemes(TestAuthHandler.SchemeName)
                    .RequireAuthenticatedUser()
                    .RequireRole(IdentitySeedData.CustomerRole));
            });
        });
    }
}
