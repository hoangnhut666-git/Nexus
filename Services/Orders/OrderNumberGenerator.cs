using System.Globalization;

namespace Nexus.Services.Orders;

/// <summary>
/// Builds a human-friendly, unique order number in the form <c>NX-yyyyMMdd-000123</c>.
/// Uniqueness is guaranteed by the identity <paramref name="orderId"/>, which also backs
/// the unique index on <c>Order.OrderNumber</c>.
/// </summary>
public static class OrderNumberGenerator
{
    public static string Generate(DateTime createdAtUtc, int orderId) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"NX-{createdAtUtc:yyyyMMdd}-{orderId:D6}");
}
