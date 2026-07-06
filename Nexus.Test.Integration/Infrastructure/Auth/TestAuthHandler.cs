using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Nexus.Test.Integration.Infrastructure.Auth;

/// <summary>
/// A minimal authentication handler that auto-authenticates every request
/// as a deterministic test admin user. This bypasses ASP.NET Core Identity
/// cookie auth so integration tests can focus on business logic.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestScheme";

    /// <summary>The stable user ID seeded in <see cref="TestDatabaseFixture"/>.</summary>
    public const string TestUserId = "test-admin-user-id-00000000001";

    public const string TestUserEmail = "testadmin@integration.test";
    public const string TestUserName = "testadmin@integration.test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = TestUserId;
        var userName = TestUserName;

        if (Request.Headers.TryGetValue("X-Test-UserId", out var userIdValues)
            && !string.IsNullOrWhiteSpace(userIdValues.ToString()))
        {
            userId = userIdValues.ToString();
            userName = Request.Headers.TryGetValue("X-Test-UserName", out var nameValues)
                       && !string.IsNullOrWhiteSpace(nameValues.ToString())
                ? nameValues.ToString()
                : userId;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.Email, TestUserEmail),
            new(ClaimTypes.Role, IdentitySeedData.AdminRole),
            new("sub", userId),
        };

        if (Request.Headers.TryGetValue("X-Test-Roles", out var roleValues))
        {
            claims.RemoveAll(c => c.Type == ClaimTypes.Role);
            foreach (var role in roleValues.ToString()
                         .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }

        var identity = new ClaimsIdentity(claims.ToArray(), SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
