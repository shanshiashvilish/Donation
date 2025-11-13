using Donation.Api.Middlewares;
using Donation.Core.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Donation.Api.Controllers;

[ServiceFilter(typeof(ValidateModelFilter))]
[ApiController]
[Route("[controller]")]
public sealed class WebhookController(ISubscriptionService subscriptionService, ILogger<WebhookController> logger) : ControllerBase
{
    /// Flitt will POST JSON here after checkout
    [HttpPost("flitt/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(CancellationToken ct)
    {
        try
        {
            var body = await ReadBodyAsync(Request, ct);
            if (string.IsNullOrWhiteSpace(body))
            {
                logger.LogWarning("Flitt webhook: empty body.");
                return BadRequest("Invalid payload");
            }

            if (!TryParseJsonToDictionary(body, out var requestData))
            {
                logger.LogWarning("Flitt webhook: malformed JSON.");
                return BadRequest("Invalid payload");
            }

            logger.LogInformation("Flitt webhook received: keys={Keys}", string.Join(", ", requestData.Keys));

            await subscriptionService.HandleFlittCallbackAsync(requestData, ct);

            logger.LogInformation("Flitt webhook processed successfully. order_id={OrderId}",
                requestData.TryGetValue("order_id", out var oid) ? oid : "unknown");

            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Flitt webhook processing failed.");
            return BadRequest();
        }
    }


    #region Private Methods

    private static async Task<string> ReadBodyAsync(HttpRequest request, CancellationToken ct)
    {
        if (request.Body == null) return string.Empty;
        using var reader = new StreamReader(request.Body);
        return await reader.ReadToEndAsync(ct);
    }

    private static bool TryParseJsonToDictionary(string json, out Dictionary<string, string> data)
    {
        data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var v = prop.Value;
                var value = v.ValueKind switch
                {
                    JsonValueKind.String => v.GetString() ?? string.Empty,
                    JsonValueKind.Number => v.GetRawText(),
                    JsonValueKind.True or JsonValueKind.False => v.GetRawText(),
                    JsonValueKind.Null => string.Empty,
                    _ => v.GetRawText()
                };
                data[prop.Name] = value ?? string.Empty;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
