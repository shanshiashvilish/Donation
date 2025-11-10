using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using Donation.Core.Enums;
using Donation.Core.Subscriptions;
using Microsoft.Extensions.Options;

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

    public async Task<(string checkoutUrl, string orderId, string externalId)> SubscribeAsync(
        int amountMinor, string email, string name, string lastName, CancellationToken ct = default)
    {
        var orderId = Guid.NewGuid().ToString("N");
        var currency = Currency.GEL.ToString().ToUpperInvariant();

        var recurringData = BuildRecurringData(amountMinor);

        var merchantPayload = new
        {
            orderId,
            email,
            name,
            lastName,
        };

        var merchantDataJson = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(merchantPayload))
        );

        var reqParams = new Dictionary<string, object?>(StringComparer.Ordinal)
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
            ["merchant_data"] = merchantDataJson
        };

        reqParams["signature"] = BuildSha1(_options.SecretKey, reqParams);

        var payload = new { request = reqParams };

        using var resp = await _httpClient.PostAsJsonAsync(_options.CheckoutEndpoint, payload, ct);
        resp.EnsureSuccessStatusCode();

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
                  ?? throw new InvalidOperationException("Flitt empty response.");

        if (!doc.RootElement.TryGetProperty("response", out var root))
            throw new InvalidOperationException($"Flitt malformed response: {doc.RootElement}");

        var status = root.GetProperty("response_status").GetString();
        if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
        {
            var code = root.TryGetProperty("error_code", out var ec) ? ec.GetString() : null;
            var msg = root.TryGetProperty("error_message", out var em) ? em.GetString() : null;
            throw new InvalidOperationException($"Flitt create order failed: {code} {msg} | {root}");
        }

        var checkoutUrl = root.GetProperty("checkout_url").GetString();
        if (string.IsNullOrWhiteSpace(checkoutUrl))
            throw new InvalidOperationException("Flitt response missing checkout_url");

        var externalId = root.GetProperty("payment_id").GetString();
        if (string.IsNullOrWhiteSpace(externalId))
            throw new InvalidOperationException("Flitt response missing payment_id");

        return (checkoutUrl!, orderId, externalId);
    }

    public async Task<bool> UnsubscribeAsync(string externalId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(externalId)) throw new ArgumentNullException(nameof(externalId));

        var reqParams = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["order_id"] = externalId.Trim(),
            ["merchant_id"] = _options.MerchantId,
            ["action"] = "stop"
        };
        reqParams["signature"] = BuildSha1(_options.SecretKey, reqParams);

        var payload = new { request = reqParams };

        using var resp = await _httpClient.PostAsJsonAsync(_options.SubscriptionEndpoint, payload, ct);
        resp.EnsureSuccessStatusCode();

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
        if (doc is null || !doc.RootElement.TryGetProperty("response", out var root)) return false;

        var responseStatus = root.GetProperty("response_status").GetString() ?? "failure";
        var result = string.Equals(responseStatus, "success", StringComparison.OrdinalIgnoreCase);

        return result;
    }

    public bool VerifySignature(IDictionary<string, string?> callback)
    {
        var secret = _options.SecretKey;

        // 2) Build list: skip empty values, and exclude 'signature' & 'response_signature_string'
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
        return string.Equals(incoming, computed, StringComparison.Ordinal);
    }

    #region Private Methods

    private static Dictionary<string, object?> BuildRecurringData(int amountMinor)
    {
        var period = SubscriptionPeriod.Month.ToString().ToLowerInvariant();

        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["amount"] = amountMinor,
            ["every"] = 1,
            ["period"] = period,
            ["state"] = "hidden",
            ["readonly"] = "Y",
            ["quantity"] = 12,
        };

        return data;
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
                IDictionary<string, object?> dict => SerializeFlittJson(dict),
                IEnumerable<object?> list => SerializeFlittJson(list),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => kv.Value?.ToString()
            };

            if (!string.IsNullOrWhiteSpace(valueString))
                parts.Add(valueString);
        }

        var joined = string.Join("|", parts);
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(joined));
        var result = string.Concat(hash.Select(b => b.ToString("x2")));

        return result;
    }

    /// Serializes an object so primitives are stringified, then formats JSON with spaces
    /// after ':' and ',' and swaps quotes to single quotes.
    private static string SerializeFlittJson(object obj)
    {
        var normalized = NormalizeForFlitt(obj);
        var json = JsonSerializer.Serialize(normalized);

        // Insert a space after ':' and ',' only when it's missing.
        // Safe here because our values won't contain ':' or ',' characters.
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
