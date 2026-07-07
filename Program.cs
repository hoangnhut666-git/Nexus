using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nexus.Components;
using Nexus.Components.Account;
using Nexus.Data;
using Nexus.Services.Cart;
using Nexus.Services.Categories;
using Nexus.Services.Orders;
using Nexus.Services.Products;

var builder = WebApplication.CreateBuilder(args);

var identitySettings = builder.Configuration
    .GetSection(IdentitySettings.SectionName)
    .Get<IdentitySettings>() ?? new IdentitySettings();

builder.Services.Configure<IdentitySettings>(
    builder.Configuration.GetSection(IdentitySettings.SectionName));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddScoped<ApplicationDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = identitySettings.RequireConfirmedAccount;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        options.Password.RequiredLength = 6;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(IdentitySeedData.AdminRole));

    options.AddPolicy("Customer", policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(IdentitySeedData.CustomerRole));
});

builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ICategoryImageService, CategoryImageService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IProductImageService, ProductImageService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<CartState>();
builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();

// Ensure the database schema exists (applies pending migrations on a fresh database),
// then seed identity accounts and the product catalog so data survives a database switch.
// The integration test harness sets RunStartupMigrations=false and owns schema/seed data
// via its fixture, so this startup bootstrapping only runs for the real application.
if (builder.Configuration.GetValue("RunStartupMigrations", true))
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    await IdentitySeedData.SeedAsync(app.Services);
    await CatalogSeedData.SeedAsync(app.Services);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
