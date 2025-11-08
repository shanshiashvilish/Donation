using Donation.Core.Enums;
using Donation.Core.Subscriptions;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Donation.Infrastructure.Clients.Flitt;

internal sealed class FlittClient : IFlittClient
{
    private readonly HttpClient _httpClient;
    private readonly FlittOptions _options;

    public FlittClient(HttpClient httpClient, IOptions<FlittOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<(string checkoutUrl, string orderId)> SubscribeAsync(int amountMinor, string email, string name, string lastName, CancellationToken ct = default)
    {
        var orderId = Guid.NewGuid().ToString("N");
        var currency = Currency.GEL.ToString().ToUpper();

        var reqParams = new Dictionary<string, object?>
        {
            ["order_id"] = orderId,
            ["merchant_id"] = _options.MerchantId,
            ["amount"] = amountMinor,
            ["currency"] = currency,
            ["response_url"] = _options.ResponseUrl,
            ["server_callback_url"] = _options.ServerCallbackUrl,
            ["subscription"] = "Y",
            ["subscription_callback_url"] = _options.SubscriptionCallbackUrl,
            ["sender_email"] = email,
            ["customer_first_name"] = name,
            ["customer_last_name"] = lastName,
        };

        reqParams["signature"] = BuildSha1(_options.SecretKey, reqParams);

        var payload = new { request = reqParams };

        using var resp = await _httpClient.PostAsJsonAsync(_options.CheckoutEndpoint, payload, ct);
        resp.EnsureSuccessStatusCode();

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
        var root = doc!.RootElement.GetProperty("response");

        var status = root.GetProperty("response_status").GetString();
        if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Flitt create order failed: {root}");

        var checkoutUrl = root.GetProperty("checkout_url").GetString();
        if (string.IsNullOrWhiteSpace(checkoutUrl))
            throw new InvalidOperationException("Flitt response missing checkout_url");

        return (checkoutUrl!, orderId);
    }

    public async Task<bool> UnsubscribeAsync(string externalId, CancellationToken ct = default)
    {
        var reqParams = new Dictionary<string, object?>
        {
            ["order_id"] = externalId,
            ["merchant_id"] = _options.MerchantId,
            ["action"] = "stop"
        };
        reqParams["signature"] = BuildSha1(_options.SecretKey, reqParams);

        var payload = new { request = reqParams };

        using var resp = await _httpClient.PostAsJsonAsync(_options.SubscriptionEndpoint, payload, ct);
        resp.EnsureSuccessStatusCode();

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
        var root = doc!.RootElement.GetProperty("response");

        var responseStatus = root.GetProperty("response_status").GetString() ?? "failure";
        return string.Equals(responseStatus, "success", StringComparison.OrdinalIgnoreCase);
    }

    public bool VerifySignature(IDictionary<string, string?> responseOrCallback)
    {
        // Flitt posts a JSON with "response": { ... }; flatten it before calling or pass the "response" dict directly.
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in responseOrCallback)
            dict[kv.Key] = kv.Value;

        if (!responseOrCallback.TryGetValue("signature", out var sig) || string.IsNullOrWhiteSpace(sig))
            return false;

        var expected = BuildSha1(_options.SecretKey, dict);
        return string.Equals(sig, expected, StringComparison.OrdinalIgnoreCase);
    }

    #region Private Methods

    private static string BuildSha1(string secret, IDictionary<string, object?> parameters)
    {
        var parts = new List<string> { secret };
        foreach (var kv in parameters
                 .Where(kv => kv.Value is not null && kv.Key != "signature")
                 .OrderBy(kv => kv.Key))
        {
            var v = kv.Value?.ToString();
            if (!string.IsNullOrEmpty(v)) parts.Add(v!);
        }
        var s = string.Join("|", parts);
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(s));
        return string.Concat(hash.Select(b => b.ToString("x2")));
    }

    #endregion

}
