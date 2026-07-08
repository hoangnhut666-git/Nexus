using Nexus.Services.Addresses.Models;

namespace Nexus.Services.Addresses;

public static class AddressFormatter
{
    public static string OneLine(AddressDto a)
    {
        var parts = new List<string> { a.AddressLine, a.Ward, a.Province };

        if (!string.Equals(a.Country, "Vietnam", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(a.Country))
            parts.Add(a.Country);

        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
