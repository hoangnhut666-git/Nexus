using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;

namespace Nexus.Services.Addresses;

public sealed class VietnamAddressUnitService : IVietnamAddressUnitService
{
    private const string RelativePath = "data/vn-admin-units.json";

    private readonly IReadOnlyList<string> _provinces;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _wardsByProvince;
    private readonly IReadOnlyDictionary<string, HashSet<string>> _validWards;

    public VietnamAddressUnitService(IWebHostEnvironment environment)
    {
        var root = string.IsNullOrEmpty(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath;

        var path = Path.Combine(root, RelativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Vietnamese administrative-unit dataset not found at '{path}'.", path);

        using var stream = File.OpenRead(path);
        var units = JsonSerializer.Deserialize<List<ProvinceUnit>>(stream)
                    ?? throw new InvalidOperationException("Administrative-unit dataset is empty or invalid.");

        var provinces = new List<string>(units.Count);
        var wardsByProvince = new Dictionary<string, IReadOnlyList<string>>();
        var validWards = new Dictionary<string, HashSet<string>>();

        foreach (var unit in units)
        {
            var province = Normalize(unit.Province);
            if (province.Length == 0)
                continue;

            var wards = (unit.Wards ?? [])
                .Select(Normalize)
                .Where(w => w.Length > 0)
                .ToList();

            provinces.Add(province);
            wardsByProvince[province] = wards;
            validWards[province] = [.. wards];
        }

        _provinces = provinces;
        _wardsByProvince = wardsByProvince;
        _validWards = validWards;
    }

    public IReadOnlyList<string> GetProvinces() => _provinces;

    public IReadOnlyList<string> GetWards(string province)
    {
        var key = Normalize(province);
        return _wardsByProvince.TryGetValue(key, out var wards) ? wards : [];
    }

    public bool IsValid(string province, string ward)
    {
        var provinceKey = Normalize(province);
        var wardKey = Normalize(ward);

        return provinceKey.Length > 0
               && wardKey.Length > 0
               && _validWards.TryGetValue(provinceKey, out var wards)
               && wards.Contains(wardKey);
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Normalize(NormalizationForm.FormC);

    private sealed class ProvinceUnit
    {
        [JsonPropertyName("province")]
        public string Province { get; set; } = string.Empty;

        [JsonPropertyName("wards")]
        public List<string>? Wards { get; set; }
    }
}
