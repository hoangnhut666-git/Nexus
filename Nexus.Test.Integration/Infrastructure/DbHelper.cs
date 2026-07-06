namespace Nexus.Test.Integration.Infrastructure;

/// <summary>
/// Provides a standalone <see cref="ApplicationDbContext"/> for direct database assertions.
/// Using a separate context ensures you read persisted state, not a scoped factory cache.
/// </summary>
public sealed class DbHelper(string connectionString)
{
    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    public async Task<bool> UserExistsAsync(string userId)
    {
        await using var db = CreateContext();
        return await db.Users.AnyAsync(u => u.Id == userId);
    }

    public async Task<ApplicationUser?> GetUserAsync(string email)
    {
        await using var db = CreateContext();
        return await db.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<bool> RoleExistsAsync(string roleName)
    {
        await using var db = CreateContext();
        return await db.Roles.AnyAsync(r => r.Name == roleName);
    }
}
