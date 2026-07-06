using Bogus;
using Nexus.Test.Integration.Infrastructure.Auth;

namespace Nexus.Test.Integration.TestData;

/// <summary>
/// Bogus-based test data builders for integration tests.
/// </summary>
public static class TestDataBuilders
{
    public static Faker<ApplicationUser> ApplicationUserFaker() => new Faker<ApplicationUser>()
        .RuleFor(u => u.UserName, f => f.Internet.Email())
        .RuleFor(u => u.Email, (f, u) => u.UserName)
        .RuleFor(u => u.FullName, f => f.Name.FullName())
        .RuleFor(u => u.EmailConfirmed, _ => true);

    public static ApplicationUser ValidApplicationUser() => ApplicationUserFaker().Generate();

    public static (ApplicationUser User, string Password) ValidApplicationUserWithPassword()
    {
        var password = "Integration@123!";
        var user = ApplicationUserFaker().Generate();
        return (user, password);
    }

    public static ApplicationUser ValidTestAdminUser() => new()
    {
        Id = TestAuthHandler.TestUserId,
        UserName = TestAuthHandler.TestUserEmail,
        Email = TestAuthHandler.TestUserEmail,
        FullName = "Test Admin",
        EmailConfirmed = true,
    };
}
