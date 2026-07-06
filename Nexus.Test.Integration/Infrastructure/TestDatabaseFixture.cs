using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Testcontainers.MsSql;

namespace Nexus.Test.Integration.Infrastructure;

/// <summary>
/// Starts a real SQL Server container once per test class, applies EF migrations,
/// and resets data between tests using Respawn.
/// Prerequisites: Docker Desktop must be running; Node.js + npm install for Tailwind build.
/// Verify with: dotnet test Nexus.slnx
/// </summary>
public sealed class TestDatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private Respawner _respawner = null!;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();

        await using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                         .UseSqlServer(ConnectionString)
                         .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
                         .Options))
        {
            await db.Database.MigrateAsync();
        }

        await using var factory = new TestWebApplicationFactory(ConnectionString);
        await using var scope = factory.Services.CreateAsyncScope();

        await SeedBaseDataAsync(scope.ServiceProvider);

        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            TablesToIgnore =
            [
                new Respawn.Graph.Table("AspNetRoles"),
                new Respawn.Graph.Table("AspNetUsers"),
                new Respawn.Graph.Table("AspNetUserRoles"),
                new Respawn.Graph.Table("AspNetUserClaims"),
            ],
            DbAdapter = DbAdapter.SqlServer,
        });
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public async Task ResetAsync()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    private static async Task SeedBaseDataAsync(IServiceProvider sp)
    {
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { IdentitySeedData.AdminRole, IdentitySeedData.CustomerRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        if (await userManager.FindByIdAsync(TestAuthHandler.TestUserId) is null)
        {
            var user = new ApplicationUser
            {
                Id = TestAuthHandler.TestUserId,
                UserName = TestAuthHandler.TestUserEmail,
                Email = TestAuthHandler.TestUserEmail,
                EmailConfirmed = true,
                FullName = "Test Admin",
            };
            await userManager.CreateAsync(user, "Integration@123!");
            await userManager.AddToRoleAsync(user, IdentitySeedData.AdminRole);
        }
    }
}
