using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Nexus.Data;

public static class IdentitySeedData
{
    public const string AdminRole = "Admin";
    public const string CustomerRole = "Customer";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<IdentitySettings>>().Value;

        await EnsureRoleAsync(roleManager, AdminRole);
        await EnsureRoleAsync(roleManager, CustomerRole);

        if (string.IsNullOrWhiteSpace(settings.AdminPassword))
        {
            return;
        }

        var admin = await userManager.FindByEmailAsync(settings.AdminEmail);
        if (admin is not null)
        {
            return;
        }

        admin = new ApplicationUser
        {
            UserName = settings.AdminEmail,
            Email = settings.AdminEmail,
            FullName = settings.AdminFullName,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, settings.AdminPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        await userManager.AddToRoleAsync(admin, AdminRole);
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
