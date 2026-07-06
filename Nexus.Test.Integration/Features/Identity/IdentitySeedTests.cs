namespace Nexus.Test.Integration.Features.Identity;

/// <summary>
/// Verifies that the test database fixture seeds roles and the deterministic test admin user.
/// </summary>
public sealed class IdentitySeedTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;
    private readonly DbHelper _db;

    public IdentitySeedTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _factory = new TestWebApplicationFactory(fixture.ConnectionString);
        _db = new DbHelper(fixture.ConnectionString);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ResetAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task SeedData_AdminRoleExists()
    {
        // Act
        var exists = await _db.RoleExistsAsync(IdentitySeedData.AdminRole);

        // Assert
        exists.Should().BeTrue("the Admin role should be seeded during fixture initialization");
    }

    [Fact]
    public async Task SeedData_CustomerRoleExists()
    {
        // Act
        var exists = await _db.RoleExistsAsync(IdentitySeedData.CustomerRole);

        // Assert
        exists.Should().BeTrue("the Customer role should be seeded during fixture initialization");
    }

    [Fact]
    public async Task SeedData_TestAdminUserExists()
    {
        // Act
        var exists = await _db.UserExistsAsync(TestAuthHandler.TestUserId);
        var user = await _db.GetUserAsync(TestAuthHandler.TestUserEmail);

        // Assert
        exists.Should().BeTrue();
        user.Should().NotBeNull();
        user!.FullName.Should().Be("Test Admin");
        user.EmailConfirmed.Should().BeTrue();
    }
}
