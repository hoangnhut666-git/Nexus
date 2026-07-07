using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Nexus.Data;

public static class IdentitySeedData
{
    public const string AdminRole = "Admin";
    public const string CustomerRole = "Customer";

    // Default password applied to every seeded account. Meets the configured
    // Identity policy (>= 6 chars, upper/lower/digit/non-alphanumeric).
    public const string DefaultSeedPassword = "Nexus@123";

    private static readonly (string Email, string FullName)[] AdminAccounts =
    [
        ("admin1@nexus.com", "Trần Hoàng Nhựt"),
        ("admin2@nexus.com", "Nguyễn Mỹ Linh"),
        ("admin3@nexus.com", "Hà Đoan Trang"),
    ];

    private static readonly (string Email, string FullName)[] CustomerAccounts =
    [
        ("customer1@nexus.com", "Nguyễn Thị Thảo"),
        ("customer2@nexus.com", "Lê Thị Thu Hà"),
        ("customer3@nexus.com", "Trần Văn Bình"),
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<IdentitySettings>>().Value;

        await EnsureRoleAsync(roleManager, AdminRole);
        await EnsureRoleAsync(roleManager, CustomerRole);

        // Optional administrator configured via IdentitySettings (only when a password is supplied).
        if (!string.IsNullOrWhiteSpace(settings.AdminPassword))
        {
            await EnsureUserAsync(
                userManager,
                settings.AdminEmail,
                settings.AdminFullName,
                AdminRole,
                settings.AdminPassword);
        }

        foreach (var (email, fullName) in AdminAccounts)
        {
            await EnsureUserAsync(userManager, email, fullName, AdminRole, DefaultSeedPassword);
        }

        foreach (var (email, fullName) in CustomerAccounts)
        {
            await EnsureUserAsync(userManager, email, fullName, CustomerRole, DefaultSeedPassword);
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string role,
        string password)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed user '{email}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        await userManager.AddToRoleAsync(user, role);
    }

    private static async Task EnsureRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to seed role '{roleName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
    }
}
