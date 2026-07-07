using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Services.Categories;
using Nexus.Services.Categories.Models;
using Nexus.Test.Integration.TestData;

namespace Nexus.Test.Integration.Features.Category;

public sealed class CategoryServiceTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;
    private readonly DbHelper _dbHelper;

    public CategoryServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _factory = new TestWebApplicationFactory(fixture.ConnectionString);
        _dbHelper = new DbHelper(fixture.ConnectionString);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ResetAsync();
        await _factory.DisposeAsync();
    }

    private async Task<ICategoryService> CreateServiceAsync()
    {
        var scope = _factory.Services.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<ICategoryService>();
    }

    private async Task<ICategoryImageService> CreateImageServiceAsync()
    {
        var scope = _factory.Services.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<ICategoryImageService>();
    }

    [Fact]
    public async Task CreateAsync_PersistsCategoryWithSlugAndStatus()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

        var result = await service.CreateAsync(new CreateCategoryRequest
        {
            Name = "Mechanical & Gaming",
            Slug = "mechanical-gaming",
            Description = "Gaming peripherals",
            IsActive = true
        });

        result.Success.Should().BeTrue();
        result.Data!.Name.Should().Be("Mechanical & Gaming");
        result.Data.Slug.Should().Be("mechanical-gaming");
        result.Data.IsActive.Should().BeTrue();

        var persisted = await _dbHelper.GetCategoryBySlugAsync("mechanical-gaming");
        persisted.Should().NotBeNull();
        persisted!.Description.Should().Be("Gaming peripherals");
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateSlug_ReturnsFailure()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

        await service.CreateAsync(new CreateCategoryRequest
        {
            Name = "Audio",
            Slug = "premium-audio",
            IsActive = true
        });

        var duplicate = await service.CreateAsync(new CreateCategoryRequest
        {
            Name = "Other Audio",
            Slug = "premium-audio",
            IsActive = true
        });

        duplicate.Success.Should().BeFalse();
        duplicate.Error.Should().Contain("slug");
    }

    [Fact]
    public async Task GetPagedAsync_FiltersBySearchTerm()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

        await service.CreateAsync(new CreateCategoryRequest { Name = "Mechanical Keyboards", Slug = "mechanical-keyboards", IsActive = true });
        await service.CreateAsync(new CreateCategoryRequest { Name = "Premium Audio", Slug = "premium-audio", IsActive = true });

        var result = await service.GetPagedAsync(new CategoryQuery { Search = "audio", Page = 1, PageSize = 10 });

        result.Items.Should().HaveCount(1);
        result.Items[0].Slug.Should().Be("premium-audio");
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByStatus()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

        await service.CreateAsync(new CreateCategoryRequest { Name = "Visible Category", Slug = "visible", IsActive = true });
        await service.CreateAsync(new CreateCategoryRequest { Name = "Hidden Category", Slug = "hidden-cat", IsActive = false });

        var active = await service.GetPagedAsync(new CategoryQuery { Status = CategoryStatusFilter.Active, PageSize = 20 });
        var hidden = await service.GetPagedAsync(new CategoryQuery { Status = CategoryStatusFilter.Hidden, PageSize = 20 });

        active.Items.Should().Contain(c => c.Slug == "visible");
        active.Items.Should().NotContain(c => c.Slug == "hidden-cat");
        hidden.Items.Should().Contain(c => c.Slug == "hidden-cat");
    }

    [Fact]
    public async Task DeleteAsync_WhenCategoryHasNoProducts_Succeeds()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

        var created = await service.CreateAsync(new CreateCategoryRequest
        {
            Name = "Disposable",
            Slug = "disposable",
            IsActive = true
        });

        var result = await service.DeleteAsync(created.Data!.Id);

        result.Success.Should().BeTrue();
        (await _dbHelper.GetCategoryAsync(created.Data.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenCategoryHasProducts_ReturnsFailure()
    {
        var category = await _dbHelper.InsertCategoryAsync(TestDataBuilders.ValidCategoryWithSlug("With Products", "with-products"));
        await _dbHelper.InsertProductAsync(TestDataBuilders.ValidProduct(category.Id));

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

        var result = await service.DeleteAsync(category.Id);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("products");
        (await _dbHelper.GetCategoryAsync(category.Id)).Should().NotBeNull();
    }

    [Fact]
    public void GenerateSlug_HandlesVietnameseDiacritics()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();

        service.GenerateSlug("Điện thoại thông minh").Should().Be("dien-thoai-thong-minh");
    }

    [Fact]
    public async Task SaveUploadedFileAsync_SavesFileUnderUploadsDirectory()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var imageService = scope.ServiceProvider.GetRequiredService<ICategoryImageService>();

        await using var stream = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var result = await imageService.SaveUploadedFileAsync(stream, "icon.png", "image/png", stream.Length);

        result.Success.Should().BeTrue();
        result.Data.Should().StartWith("/uploads/categories/");

        var relativePath = result.Data!.TrimStart('/');
        var fullPath = Path.Combine(_factory.Server.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(fullPath).Should().BeTrue();

        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }
}
