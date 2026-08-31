using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nexus.Data.Entities;

namespace Nexus.Data;

public static class IdentitySeedData
{
    public const string AdminRole = "Admin";
    public const string CustomerRole = "Customer";

    // Default password applied to every seeded account. Meets the configured
    // Identity policy (>= 6 chars, upper/lower/digit/non-alphanumeric).
    public const string DefaultSeedPassword = "Abc123456@";

    private static readonly (string Email, string FullName)[] AdminAccounts =
    [
        ("admin1@test.com", "Trần Hoàng Nhựt"),
        ("admin2@test.com", "Nguyễn Châu Ngọc Mai"),
        ("admin3@test.com", "Hà Đoan Trang"),
        ("admin4@test.com", "Nguyễn Thị Ngọc Bích"),
        ("admin5@test.com", "Nguyễn Mỹ Linh"),
    ];

    private static readonly (string Email, string FullName)[] CustomerAccounts =
    [
        ("customer1@test.com", "Nguyễn Thị Thảo"),
        ("customer2@test.com", "Lê Thị Thu Hà"),
        ("customer3@test.com", "Trần Văn Bình"),
        ("customer4@test.com", "Hà Đoan Thệ"),
        ("customer5@test.com", "Nguyễn Hà Giang"),
        ("customer6@test.com", "Cao Yên Nhi"),
        ("customer7@test.com", "Nguyễn Thị Ngọc Bích"),
        ("customer8@test.com", "Nguyễn Thị Ngọc Trang"),
        ("customer9@test.com", "Nguyễn Thị Ngọc Thư"),
        ("customer10@test.com", "Nguyễn Châu Ngọc Mai"),
    ];

    // Province/ward pairs must match wwwroot/data/vn-admin-units.json so checkout dropdowns accept them.
    private static readonly Dictionary<string, SeedAddress[]> CustomerAddressBook = new(StringComparer.OrdinalIgnoreCase)
    {
        ["customer1@test.com"] =
        [
            new("25 Nguyễn Trãi", "Phường Tân An", "Tp Cần Thơ", "0901111001", "Home", true),
            new("12 Trần Hưng Đạo", "Phường Ninh Kiều", "Tp Cần Thơ", "0901111001", "Office", false),
        ],
        ["customer2@test.com"] =
        [
            new("48 Lý Thái Tổ", "Phường Hoàn Kiếm", "Hà Nội", "0901111002", "Home", true),
        ],
        ["customer3@test.com"] =
        [
            new("15 Lê Lợi", "Phường Bến Thành", "Tp Hồ Chí Minh", "0901111003", "Home", true),
            new("88 Nguyễn Huệ", "Phường Sài Gòn", "Tp Hồ Chí Minh", "0901111003", "Office", false),
        ],
        ["customer4@test.com"] =
        [
            new("7 Bạch Đằng", "Phường Hải Châu", "Tp Đà Nẵng", "0901111004", "Home", true),
        ],
        ["customer5@test.com"] =
        [
            new("32 Lê Lợi", "Phường Thuận Hóa", "Huế", "0901111005", "Home", true),
        ],
        ["customer6@test.com"] =
        [
            new("19 Trần Phú", "Phường Nha Trang", "Khánh Hòa", "0901111006", "Home", true),
        ],
        ["customer7@test.com"] =
        [
            new("56 Phạm Văn Thuận", "Phường Biên Hoà", "Đồng Nai", "0901111007", "Home", true),
        ],
        ["customer8@test.com"] =
        [
            new("21 Nguyễn Thái Học", "Phường Long Xuyên", "An Giang", "0901111008", "Home", true),
        ],
        ["customer9@test.com"] =
        [
            new("9 Phan Đình Phùng", "Phường Xuân Hương - Đà Lạt", "Lâm Đồng", "0901111009", "Home", true),
        ],
        ["customer10@test.com"] =
        [
            new("18 Nguyễn Huệ", "Phường Cao Lãnh", "Đồng Tháp", "0901111010", "Home", true),
        ],
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<IdentitySettings>>().Value;
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

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

        await using var context = await dbFactory.CreateDbContextAsync();

        foreach (var (email, fullName) in CustomerAccounts)
        {
            var user = await EnsureUserAsync(userManager, email, fullName, CustomerRole, DefaultSeedPassword);
            await EnsureCustomerAddressesAsync(context, user);
        }

        await context.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string role,
        string password)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return existing;
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
        return user;
    }

    private static async Task EnsureCustomerAddressesAsync(ApplicationDbContext context, ApplicationUser user)
    {
        if (!CustomerAddressBook.TryGetValue(user.Email!, out var specs))
        {
            return;
        }

        var hasAddresses = await context.UserAddresses.AnyAsync(a => a.UserId == user.Id);
        if (hasAddresses)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var recipient = string.IsNullOrWhiteSpace(user.FullName) ? user.Email! : user.FullName!;

        foreach (var spec in specs)
        {
            context.UserAddresses.Add(new UserAddress
            {
                UserId = user.Id,
                RecipientName = recipient,
                Phone = spec.Phone,
                AddressLine = spec.AddressLine,
                Ward = spec.Ward,
                Province = spec.Province,
                Country = "Vietnam",
                Label = spec.Label,
                IsDefault = spec.IsDefault,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
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

    private sealed record SeedAddress(
        string AddressLine,
        string Ward,
        string Province,
        string Phone,
        string Label,
        bool IsDefault);
}
