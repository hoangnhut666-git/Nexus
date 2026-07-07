namespace Nexus.Services.Payments;

public sealed class PayPalOptions
{
    public const string SectionName = "PayPal";

    /// <summary>PayPal REST API base URL. Sandbox by default; use https://api-m.paypal.com for live.</summary>
    public string BaseUrl { get; set; } = "https://api-m.sandbox.paypal.com";

    /// <summary>REST app client id. Loaded from user secrets / environment, never committed.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>REST app secret. Loaded from user secrets / environment, never committed.</summary>
    public string Secret { get; set; } = string.Empty;
}
