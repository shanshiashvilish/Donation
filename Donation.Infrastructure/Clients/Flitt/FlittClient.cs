using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using Donation.Core.Enums;
using Donation.Core.Subscriptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Donation.Infrastructure.Clients.Flitt;

internal sealed class FlittClient(HttpClient httpClient, IOptions<FlittOptions> options, ILogger<FlittClient> logger) : IFlittClient
{
    private readonly FlittOptions _options = options.Value;
    private const string Success = "success";

    public async Task<(string checkoutUrl, string orderId, string externalId)> SubscribeAsync(
        int amountMinor, string email, string name, string lastName, CancellationToken ct = default)
    {
        var orderId = Guid.NewGuid().ToString("N");
        var currency = Currency.GEL.ToString().ToUpperInvariant();

        logger.LogInformation("Flitt SubscribeAsync start: {email}, amount={Amount}, orderId={OrderId}",
            email, amountMinor, orderId);

        var merchantDataB64 = BuildMerchantDataB64(orderId, email, name, lastName);
        var recurringData = BuildRecurringData(amountMinor);

        var reqParams = BuildRequestParams(orderId, amountMinor, currency, recurringData, merchantDataB64, email, name, lastName);
        reqParams["signature"] = BuildSha1(_options.SecretKey, reqParams);

        var payload = new { request = reqParams };

        using var resp = await httpClient.PostAsJsonAsync(_options.CheckoutEndpoint, payload, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
                     ?? throw new InvalidOperationException("Flitt empty response.");

        var root = RequireResponseRoot(doc);

        EnsureSuccess(root, "Flitt create order failed");

        var checkoutUrl = root.GetProperty("checkout_url").GetString();
        if (string.IsNullOrWhiteSpace(checkoutUrl))
            throw new InvalidOperationException("Flitt response missing checkout_url");

        var externalId = root.GetProperty("payment_id").GetString();
        if (string.IsNullOrWhiteSpace(externalId))
            throw new InvalidOperationException("Flitt response missing payment_id");

        logger.LogInformation("Flitt SubscribeAsync ok: orderId={OrderId}, paymentId={PaymentId}", orderId, externalId);
        return (checkoutUrl!, orderId, externalId);
    }

    public async Task<bool> UnsubscribeAsync(string externalId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(externalId)) throw new ArgumentNullException(nameof(externalId));

        logger.LogInformation("Flitt UnsubscribeAsync start: externalId={ExternalId}", externalId);

        var reqParams = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["order_id"] = externalId.Trim(),
            ["merchant_id"] = _options.MerchantId,
            ["action"] = "stop"
        };
        reqParams["signature"] = BuildSha1(_options.SecretKey, reqParams);

        var payload = new { request = reqParams };

        using var resp = await httpClient.PostAsJsonAsync(_options.SubscriptionEndpoint, payload, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
        if (doc is null || !doc.RootElement.TryGetProperty("response", out var root))
        {
            logger.LogWarning("Flitt UnsubscribeAsync malformed/empty response for {ExternalId}", externalId);
            return false;
        }

        var ok = string.Equals(root.GetProperty("response_status").GetString() ?? "failure", Success, StringComparison.OrdinalIgnoreCase);
        logger.LogInformation("Flitt UnsubscribeAsync result for {ExternalId}: {Result}", externalId, ok ? "success" : "failure");
        return ok;
    }

    public bool VerifySignature(IDictionary<string, string?> callback)
    {
        var secret = _options.SecretKey;

        var filtered = new List<(string Key, string Val)>();
        foreach (var kv in callback)
        {
            if (kv.Key.Equals("signature", StringComparison.OrdinalIgnoreCase)) continue;
            if (kv.Key.Equals("response_signature_string", StringComparison.OrdinalIgnoreCase)) continue;

            var val = kv.Value ?? string.Empty;
            if (val.Length == 0) continue;

            filtered.Add((kv.Key, val));
        }

        filtered.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));

        var parts = new List<string>(filtered.Count + 1) { secret };
        parts.AddRange(filtered.Select(f => f.Val));
        var signatureBase = string.Join("|", parts);

        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(signatureBase));
        var computed = Convert.ToHexStringLower(hash);

        var incoming = callback.TryGetValue("signature", out var s) ? s : null;
        var match = string.Equals(incoming, computed, StringComparison.Ordinal);

        logger.LogDebug("Flitt VerifySignature: match={Match}", match);
        return match;
    }


    #region Private Methods

    private static string BuildMerchantDataB64(string orderId, string email, string name, string lastName)
    {
        var merchantPayload = new { orderId, email, name, lastName };
        var raw = JsonSerializer.Serialize(merchantPayload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    private Dictionary<string, object?> BuildRequestParams(string orderId, int amountMinor, string currency, Dictionary<string, object?> recurringData,
                                                           string merchantDataB64, string email, string name, string lastName)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["order_id"] = orderId,
            ["merchant_id"] = _options.MerchantId,
            ["amount"] = amountMinor,
            ["currency"] = currency,
            ["response_url"] = _options.ResponseUrl,
            ["server_callback_url"] = _options.ServerCallbackUrl,
            ["subscription"] = "Y",
            ["order_desc"] = $"{email},{name},{lastName}",
            ["recurring_data"] = recurringData,
            ["merchant_data"] = merchantDataB64
        };
    }

    private static JsonElement RequireResponseRoot(JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("response", out var root))
            throw new InvalidOperationException($"Flitt malformed response: {doc.RootElement}");
        return root;
    }

    private static void EnsureSuccess(JsonElement root, string whenFailedMsg)
    {
        var status = root.GetProperty("response_status").GetString();
        if (!string.Equals(status, Success, StringComparison.OrdinalIgnoreCase))
        {
            var code = root.TryGetProperty("error_code", out var ec) ? ec.GetString() : null;
            var msg = root.TryGetProperty("error_message", out var em) ? em.GetString() : null;
            throw new InvalidOperationException($"{whenFailedMsg}: {code} {msg} | {root}");
        }
    }

    private static Dictionary<string, object?> BuildRecurringData(int amountMinor)
    {
        var period = SubscriptionPeriod.Month.ToString().ToLowerInvariant();

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["amount"] = amountMinor,
            ["every"] = 1,
            ["period"] = period,
            ["state"] = "hidden",
            ["readonly"] = "Y",
            ["quantity"] = 12,
        };
    }

    private static string BuildSha1(string secret, IDictionary<string, object?> parameters)
    {
        var parts = new List<string> { secret ?? string.Empty };

        foreach (var kv in parameters
                     .Where(kv => kv.Key != "signature" && kv.Value is not null)
                     .OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            string? valueString = kv.Value switch
            {
                string str => str,
                IDictionary<string, object?> d => SerializeFlittJson(d),
                IEnumerable<object?> list => SerializeFlittJson(list),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => kv.Value?.ToString()
            };

            if (!string.IsNullOrWhiteSpace(valueString))
                parts.Add(valueString);
        }

        var joined = string.Join("|", parts);
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(joined));
        return string.Concat(hash.Select(b => b.ToString("x2")));
    }

    /// Serializes an object so primitives are stringified, then formats JSON with spaces
    /// after ':' and ',' and swaps quotes to single quotes.
    private static string SerializeFlittJson(object obj)
    {
        var normalized = NormalizeForFlitt(obj);
        var json = JsonSerializer.Serialize(normalized);

        // Insert a space after ':' and ',' when it's missing (safe for our normalized values).
        json = Regex.Replace(json, ":(?=\\S)", ": ");
        json = Regex.Replace(json, ",(?=\\S)", ", ");

        // Switch to single quotes to match Flitt's signature format.
        return json.Replace("\"", "'");
    }

    /// Recursively converts primitives to strings (1 -> "1") so the JSON has quoted numbers.
    private static object? NormalizeForFlitt(object? value)
    {
        if (value is null) return null;
        if (value is string s) return s;

        if (value is IDictionary<string, object?> dict)
        {
            var res = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in dict)
                res[kv.Key] = NormalizeForFlitt(kv.Value);
            return res;
        }

        if (value is IEnumerable<object?> list)
            return list.Select(NormalizeForFlitt).ToList();

        if (value is IFormattable f)
            return f.ToString(null, CultureInfo.InvariantCulture);

        return value.ToString();
    }

    #endregion
}
