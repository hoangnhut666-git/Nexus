using Microsoft.Extensions.DependencyInjection;
using Nexus.Services.Addresses;

namespace Nexus.Test.Integration.Features.Addresses;

public sealed class VietnamAddressUnitServiceTests : IClassFixture<TestDatabaseFixture>, IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private readonly TestWebApplicationFactory _factory;

    public VietnamAddressUnitServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
        _factory = new TestWebApplicationFactory(fixture.ConnectionString);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ResetAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public void GetProvinces_ReturnsReformDataset()
    {
        var service = Resolve();

        var provinces = service.GetProvinces();

        provinces.Should().HaveCount(34);
        provinces.Should().Contain("Hà Nội");
        provinces.Should().Contain("Tp Cần Thơ");
    }

    [Fact]
    public void GetWards_ForKnownProvince_ReturnsWards()
    {
        var service = Resolve();

        var wards = service.GetWards("Tp Cần Thơ");

        wards.Should().NotBeEmpty();
        wards.Should().Contain("Phường Tân An");
    }

    [Fact]
    public void GetWards_ForUnknownProvince_ReturnsEmpty()
    {
        var service = Resolve();

        service.GetWards("Nowhere Province").Should().BeEmpty();
    }

    [Fact]
    public void IsValid_CanonicalPair_ReturnsTrue()
    {
        var service = Resolve();

        service.IsValid("Tp Cần Thơ", "Phường Tân An").Should().BeTrue();
    }

    [Fact]
    public void IsValid_TrimsWhitespace()
    {
        var service = Resolve();

        service.IsValid("  Tp Cần Thơ  ", "  Phường Tân An  ").Should().BeTrue();
    }

    [Fact]
    public void IsValid_UnknownValues_ReturnsFalse()
    {
        var service = Resolve();

        service.IsValid("Nowhere Province", "Nowhere Ward").Should().BeFalse();
        service.IsValid("", "").Should().BeFalse();
    }

    [Fact]
    public void IsValid_WardFromAnotherProvince_ReturnsFalse()
    {
        var service = Resolve();

        // "Phường Tân An" is a Cần Thơ ward, not a Hà Nội ward.
        service.IsValid("Hà Nội", "Phường Tân An").Should().BeFalse();
    }

    private IVietnamAddressUnitService Resolve() =>
        _factory.Services.GetRequiredService<IVietnamAddressUnitService>();
}
