using System.Security.Claims;
using Nexus.Data;
using Nexus.Services.Auth;

namespace Nexus.Test.Integration.Features.Auth;

public sealed class RoleLandingHelperTests
{
    [Fact]
    public void GetDefaultLandingPath_AdminRole_ReturnsAdminDashboard()
    {
        var principal = CreatePrincipal(IdentitySeedData.AdminRole);

        RoleLandingHelper.GetDefaultLandingPath(principal).Should().Be(RoleLandingHelper.AdminDashboard);
    }

    [Fact]
    public void GetDefaultLandingPath_CustomerRole_ReturnsCustomerHome()
    {
        var principal = CreatePrincipal(IdentitySeedData.CustomerRole);

        RoleLandingHelper.GetDefaultLandingPath(principal).Should().Be(RoleLandingHelper.CustomerHome);
    }

    [Fact]
    public void ResolveReturnUrl_WithReturnUrl_HonorsReturnUrl()
    {
        var principal = CreatePrincipal(IdentitySeedData.AdminRole);

        RoleLandingHelper.ResolveReturnUrl("/admin/products", principal).Should().Be("/admin/products");
    }

    [Fact]
    public void ResolveReturnUrl_WithoutReturnUrl_UsesRoleDefault()
    {
        var admin = CreatePrincipal(IdentitySeedData.AdminRole);
        var customer = CreatePrincipal(IdentitySeedData.CustomerRole);

        RoleLandingHelper.ResolveReturnUrl(null, admin).Should().Be("/admin");
        RoleLandingHelper.ResolveReturnUrl("", customer).Should().Be("/");
    }

    private static ClaimsPrincipal CreatePrincipal(params string[] roles)
    {
        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role)).ToList();
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }
}
