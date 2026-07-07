using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nexus.Data.Entities;
using Nexus.Services.Categories.Models;
using Nexus.Services.Payments.Models;

namespace Nexus.Services.Payments;

/// <summary>
/// PayPal Orders v2 REST integration (capture-on-return). Creates an order for the
/// order total in USD, returns the buyer approval URL, and captures on return.
/// </summary>
public sealed class PayPalPaymentGateway(
    HttpClient httpClient,
    IOptions<PayPalOptions> options) : IPaymentGateway
{
    private readonly PayPalOptions _options = options.Value;

    public PaymentMethod Method => PaymentMethod.PayPal;

    public async Task<ServiceResult<PaymentInitiation>> CreatePaymentAsync(
        Order order,
        string returnUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        if (token is null)
            return ServiceResult<PaymentInitiation>.Fail("Could not authenticate with PayPal.");

        var payload = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    reference_id = order.OrderNumber,
                    amount = new
                    {
                        currency_code = order.Currency,
                        value = order.Total.ToString("F2", CultureInfo.InvariantCulture)
                    }
                }
            },
            application_context = new
            {
                return_url = returnUrl,
                cancel_url = cancelUrl,
                shipping_preference = "NO_SHIPPING",
                user_action = "PAY_NOW"
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v2/checkout/orders")
        {
            Content = JsonContent(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ServiceResult<PaymentInitiation>.Fail("PayPal order creation failed.");

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var gatewayOrderId = root.GetProperty("id").GetString();

            string? approvalUrl = null;
            if (root.TryGetProperty("links", out var links))
            {
                foreach (var link in links.EnumerateArray())
                {
                    if (link.TryGetProperty("rel", out var rel)
                        && string.Equals(rel.GetString(), "approve", StringComparison.OrdinalIgnoreCase))
                    {
                        approvalUrl = link.GetProperty("href").GetString();
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(gatewayOrderId) || string.IsNullOrWhiteSpace(approvalUrl))
                return ServiceResult<PaymentInitiation>.Fail("PayPal did not return an approval link.");

            return ServiceResult<PaymentInitiation>.Ok(new PaymentInitiation(gatewayOrderId, approvalUrl));
        }
        catch (JsonException)
        {
            return ServiceResult<PaymentInitiation>.Fail("Could not read the PayPal response.");
        }
    }

    public async Task<ServiceResult<PaymentResult>> CapturePaymentAsync(
        string gatewayOrderId,
        CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        if (token is null)
            return ServiceResult<PaymentResult>.Fail("Could not authenticate with PayPal.");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_options.BaseUrl}/v2/checkout/orders/{gatewayOrderId}/capture")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ServiceResult<PaymentResult>.Fail("PayPal capture failed.");

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var status = root.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;

            var capture = root
                .GetProperty("purchase_units")[0]
                .GetProperty("payments")
                .GetProperty("captures")[0];

            var captureId = capture.GetProperty("id").GetString();
            var amount = capture.GetProperty("amount");
            var value = amount.GetProperty("value").GetString();
            var currency = amount.GetProperty("currency_code").GetString() ?? "USD";

            var completed = string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase);
            var parsedAmount = decimal.Parse(value ?? "0", CultureInfo.InvariantCulture);

            return ServiceResult<PaymentResult>.Ok(new PaymentResult(
                Success: completed,
                TransactionRef: captureId,
                CapturedAmount: parsedAmount,
                Currency: currency,
                RawJson: body,
                Error: completed ? null : $"PayPal capture status: {status}"));
        }
        catch (Exception ex) when (ex is JsonException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            return ServiceResult<PaymentResult>.Fail("Could not read the PayPal capture response.");
        }
    }

    private async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.Secret}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/v1/oauth2/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials"
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("access_token", out var tokenEl)
                ? tokenEl.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static StringContent JsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
}
