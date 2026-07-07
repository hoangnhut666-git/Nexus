using Bogus;
using Nexus.Data.Entities;
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

    public static Faker<Category> CategoryFaker() => new Faker<Category>()
        .RuleFor(c => c.Name, f => f.Commerce.Categories(1)[0])
        .RuleFor(c => c.Slug, (f, c) => f.Lorem.Slug(2))
        .RuleFor(c => c.Description, f => f.Lorem.Sentence())
        .RuleFor(c => c.ImageUrl, f => f.Image.PicsumUrl())
        .RuleFor(c => c.IsActive, f => f.Random.Bool(0.8f))
        .RuleFor(c => c.CreatedAt, _ => DateTime.UtcNow)
        .RuleFor(c => c.UpdatedAt, _ => DateTime.UtcNow);

    public static Category ValidCategory() => CategoryFaker().Generate();

    public static Category ValidCategoryWithSlug(string name, string slug) => new()
    {
        Name = name,
        Slug = slug,
        Description = "Test category",
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    public static Product ValidProduct(int categoryId, string? name = null) => new()
    {
        Name = name ?? "Test Product",
        Slug = "test-product",
        Description = "Test product description",
        CategoryId = categoryId,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
