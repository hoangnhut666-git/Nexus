using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Nexus.Data;

namespace Nexus.Services.Auth;

public static class RoleLandingHelper
{
    public const string AdminDashboard = "/admin";
    public const string CustomerHome = "/";

    public static string GetDefaultLandingPath(ClaimsPrincipal user) =>
        user.IsInRole(IdentitySeedData.AdminRole) ? AdminDashboard : CustomerHome;

    public static string ResolveReturnUrl(string? returnUrl, ClaimsPrincipal user) =>
        string.IsNullOrWhiteSpace(returnUrl) ? GetDefaultLandingPath(user) : returnUrl;

    public static async Task<string> ResolveReturnUrlAsync(
        string? returnUrl,
        ApplicationUser user,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            return returnUrl;
        }

        var roles = await userManager.GetRolesAsync(user);
        var identity = new ClaimsIdentity(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return GetDefaultLandingPath(new ClaimsPrincipal(identity));
    }
}
