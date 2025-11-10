using Donation.Api.Middlewares;
using Donation.Core.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Donation.Api.Controllers;

[ServiceFilter(typeof(ValidateModelFilter))]
[ApiController]
[Route("[controller]")]
public class WebhookController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger _logger;

    public WebhookController(ISubscriptionService subscriptionService, ILogger<WebhookController> logger)
    {
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    /// Flitt will POST form-data here after checkout
    [HttpPost("flitt/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(CancellationToken ct)
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync(ct);

            var requestData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (var doc = JsonDocument.Parse(body))
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    return BadRequest("Invalid payload");

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    string valueStr = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                        JsonValueKind.Number => prop.Value.GetRawText(),
                        JsonValueKind.True or JsonValueKind.False => prop.Value.GetRawText(),
                        JsonValueKind.Null => string.Empty,
                        _ => prop.Value.GetRawText()
                    };

                    requestData[prop.Name] = valueStr ?? string.Empty;
                }
            }

            _logger.LogInformation("Flitt webhook received: {@RequestData}", requestData);

            await _subscriptionService.HandleFlittCallbackAsync(requestData, ct);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Flitt webhook failed");
            return BadRequest();
        }
    }
}
