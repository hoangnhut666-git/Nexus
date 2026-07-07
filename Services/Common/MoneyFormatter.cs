using System.Globalization;

namespace Nexus.Services.Common;

/// <summary>
/// Single source of truth for money formatting across the storefront and admin.
/// The store operates in a single currency (USD); prices render as "$1,234.56".
/// </summary>
public static class MoneyFormatter
{
    private static readonly CultureInfo Usd = CultureInfo.GetCultureInfo("en-US");

    public static string Format(decimal amount) => amount.ToString("C", Usd);
}
