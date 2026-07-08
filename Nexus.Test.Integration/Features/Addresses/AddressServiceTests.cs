using Microsoft.Extensions.DependencyInjection;
using Nexus.Services.Addresses;
using Nexus.Services.Addresses.Models;

namespace Nexus.Test.Integration.Features.Addresses;

public sealed class AddressServiceTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private const string UserA = "addr-user-a-0000000000000001";
    private const string UserB = "addr-user-b-0000000000000002";

    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;
    private readonly DbHelper _dbHelper;

    public AddressServiceTests(TestDatabaseFixture fixture)
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

    [Fact]
    public async Task AddAsync_FirstAddress_BecomesDefault()
    {
        await _dbHelper.EnsureUserAsync(UserA);

        var service = Resolve();
        var result = await service.AddAsync(UserA, ValidInput("Alice"));

        result.Success.Should().BeTrue();
        result.Data!.IsDefault.Should().BeTrue();

        var stored = await _dbHelper.GetAddressesAsync(UserA);
        stored.Should().ContainSingle();
        stored[0].IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task AddAsync_MissingRequiredField_Fails()
    {
        await _dbHelper.EnsureUserAsync(UserA);

        var service = Resolve();
        var result = await service.AddAsync(UserA, ValidInput("Alice", addressLine: ""));

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
        (await _dbHelper.GetAddressesAsync(UserA)).Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_SecondWithoutFlag_DoesNotChangeDefault()
    {
        await _dbHelper.EnsureUserAsync(UserA);

        var service = Resolve();
        var first = await service.AddAsync(UserA, ValidInput("Alice"));
        var second = await service.AddAsync(UserA, ValidInput("Bob"));

        second.Success.Should().BeTrue();
        second.Data!.IsDefault.Should().BeFalse();

        var stored = await _dbHelper.GetAddressesAsync(UserA);
        stored.Should().HaveCount(2);
        stored.Count(a => a.IsDefault).Should().Be(1);
        stored.Single(a => a.IsDefault).Id.Should().Be(first.Data!.Id);
    }

    [Fact]
    public async Task AddAsync_SecondWithSetDefault_MovesDefaultAndClearsPrevious()
    {
        await _dbHelper.EnsureUserAsync(UserA);

        var service = Resolve();
        var first = await service.AddAsync(UserA, ValidInput("Alice"));
        var second = await service.AddAsync(UserA, ValidInput("Bob", setAsDefault: true));

        second.Success.Should().BeTrue();
        second.Data!.IsDefault.Should().BeTrue();

        var stored = await _dbHelper.GetAddressesAsync(UserA);
        stored.Count(a => a.IsDefault).Should().Be(1);
        stored.Single(a => a.IsDefault).Id.Should().Be(second.Data!.Id);
        stored.Single(a => a.Id == first.Data!.Id).IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task SetDefaultAsync_SwitchesDefault_ExactlyOne()
    {
        await _dbHelper.EnsureUserAsync(UserA);

        var service = Resolve();
        var first = await service.AddAsync(UserA, ValidInput("Alice"));
        var second = await service.AddAsync(UserA, ValidInput("Bob"));

        var result = await service.SetDefaultAsync(UserA, second.Data!.Id);

        result.Success.Should().BeTrue();

        var stored = await _dbHelper.GetAddressesAsync(UserA);
        stored.Count(a => a.IsDefault).Should().Be(1);
        stored.Single(a => a.IsDefault).Id.Should().Be(second.Data!.Id);
        stored.Single(a => a.Id == first.Data!.Id).IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task SetDefaultAsync_ForeignUser_Fails()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        await _dbHelper.EnsureUserAsync(UserB);

        var service = Resolve();
        var owned = await service.AddAsync(UserA, ValidInput("Alice"));

        var result = await service.SetDefaultAsync(UserB, owned.Data!.Id);

        result.Success.Should().BeFalse();
        (await _dbHelper.GetAddressesAsync(UserB)).Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_ChangesFields()
    {
        await _dbHelper.EnsureUserAsync(UserA);

        var service = Resolve();
        var created = await service.AddAsync(UserA, ValidInput("Alice"));

        var result = await service.UpdateAsync(UserA, created.Data!.Id, ValidInput(
            "Alice Updated",
            addressLine: "No. 99, New Street",
            ward: "Phường Ba Đình",
            province: "Hà Nội"));

        result.Success.Should().BeTrue();
        result.Data!.RecipientName.Should().Be("Alice Updated");
        result.Data.AddressLine.Should().Be("No. 99, New Street");
        result.Data.Ward.Should().Be("Phường Ba Đình");
        result.Data.Province.Should().Be("Hà Nội");
    }

    [Fact]
    public async Task AddAsync_InvalidProvinceWard_Fails()
    {
        await _dbHelper.EnsureUserAsync(UserA);

        var service = Resolve();
        var result = await service.AddAsync(UserA, ValidInput(
            "Alice",
            ward: "Nowhere Ward",
            province: "Nowhere Province"));

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
        (await _dbHelper.GetAddressesAsync(UserA)).Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_InvalidProvinceWard_Fails()
    {
        await _dbHelper.EnsureUserAsync(UserA);

        var service = Resolve();
        var created = await service.AddAsync(UserA, ValidInput("Alice"));

        var result = await service.UpdateAsync(UserA, created.Data!.Id, ValidInput(
            "Alice",
            ward: "Phường Tân An",
            province: "Hà Nội")); // ward does not belong to this province

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdateAsync_ForeignUser_Fails()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        await _dbHelper.EnsureUserAsync(UserB);

        var service = Resolve();
        var owned = await service.AddAsync(UserA, ValidInput("Alice"));

        var result = await service.UpdateAsync(UserB, owned.Data!.Id, ValidInput("Hacker"));

        result.Success.Should().BeFalse();

        var stored = await _dbHelper.GetAddressesAsync(UserA);
        stored.Single().RecipientName.Should().Be("Alice");
    }

    [Fact]
    public async Task DeleteAsync_Default_PromotesMostRecentRemaining()
    {
        await _dbHelper.EnsureUserAsync(UserA);

        var service = Resolve();
        var first = await service.AddAsync(UserA, ValidInput("Alice")); // default
        await service.AddAsync(UserA, ValidInput("Bob"));
        var third = await service.AddAsync(UserA, ValidInput("Carol"));

        var result = await service.DeleteAsync(UserA, first.Data!.Id);

        result.Success.Should().BeTrue();

        var stored = await _dbHelper.GetAddressesAsync(UserA);
        stored.Should().HaveCount(2);
        stored.Count(a => a.IsDefault).Should().Be(1);
        // Carol was added last (highest UpdatedAt/Id), so it should be promoted.
        stored.Single(a => a.IsDefault).Id.Should().Be(third.Data!.Id);
    }

    [Fact]
    public async Task DeleteAsync_NonDefault_LeavesDefaultIntact()
    {
        await _dbHelper.EnsureUserAsync(UserA);

        var service = Resolve();
        var first = await service.AddAsync(UserA, ValidInput("Alice")); // default
        var second = await service.AddAsync(UserA, ValidInput("Bob"));

        var result = await service.DeleteAsync(UserA, second.Data!.Id);

        result.Success.Should().BeTrue();

        var stored = await _dbHelper.GetAddressesAsync(UserA);
        stored.Should().ContainSingle();
        stored[0].Id.Should().Be(first.Data!.Id);
        stored[0].IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ForeignUser_Fails()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        await _dbHelper.EnsureUserAsync(UserB);

        var service = Resolve();
        var owned = await service.AddAsync(UserA, ValidInput("Alice"));

        var result = await service.DeleteAsync(UserB, owned.Data!.Id);

        result.Success.Should().BeFalse();
        (await _dbHelper.GetAddressesAsync(UserA)).Should().ContainSingle();
    }

    [Fact]
    public async Task GetAddressesAsync_DefaultFirst()
    {
        await _dbHelper.EnsureUserAsync(UserA);

        var service = Resolve();
        await service.AddAsync(UserA, ValidInput("Alice"));
        var second = await service.AddAsync(UserA, ValidInput("Bob", setAsDefault: true));

        var stored = await service.GetAddressesAsync(UserA);

        stored.Should().HaveCount(2);
        stored[0].Id.Should().Be(second.Data!.Id);
        stored[0].IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsOwn_AndNullForForeignUser()
    {
        await _dbHelper.EnsureUserAsync(UserA);
        await _dbHelper.EnsureUserAsync(UserB);

        var service = Resolve();
        var created = await service.AddAsync(UserA, ValidInput("Alice"));

        var own = await service.GetByIdAsync(UserA, created.Data!.Id);
        own.Should().NotBeNull();
        own!.Id.Should().Be(created.Data!.Id);
        own.RecipientName.Should().Be("Alice");

        var foreign = await service.GetByIdAsync(UserB, created.Data!.Id);
        foreign.Should().BeNull();
    }

    [Fact]
    public async Task GetDefaultAsync_ReturnsDefaultOrNull()
    {
        await _dbHelper.EnsureUserAsync(UserA);

        var service = Resolve();

        (await service.GetDefaultAsync(UserA)).Should().BeNull();

        var created = await service.AddAsync(UserA, ValidInput("Alice"));

        var def = await service.GetDefaultAsync(UserA);
        def.Should().NotBeNull();
        def!.Id.Should().Be(created.Data!.Id);
    }

    private IAddressService Resolve()
    {
        var scope = _factory.Services.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<IAddressService>();
    }

    // Canonical province/ward pairs from the bundled dataset (strict validation).
    private const string ValidProvince = "Tp Cần Thơ";
    private const string ValidWard = "Phường Tân An";

    private static AddressInput ValidInput(
        string recipient,
        bool setAsDefault = false,
        string addressLine = "No. 25, Nguyễn Trãi Street",
        string ward = ValidWard,
        string province = ValidProvince,
        string phone = "0901234567") => new()
    {
        RecipientName = recipient,
        Phone = phone,
        AddressLine = addressLine,
        Ward = ward,
        Province = province,
        Country = "Vietnam",
        SetAsDefault = setAsDefault
    };
}
