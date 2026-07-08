namespace Nexus.Services.Addresses;

/// <summary>
/// Read-only lookup for Vietnam's 2-tier administrative units (province/city -> ward/commune),
/// sourced from the bundled dataset. Used to populate dropdowns and to validate submitted values.
/// </summary>
public interface IVietnamAddressUnitService
{
    IReadOnlyList<string> GetProvinces();

    IReadOnlyList<string> GetWards(string province);

    bool IsValid(string province, string ward);
}
