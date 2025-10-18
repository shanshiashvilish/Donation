using Donation.Api.Middlewares;
using Donation.Core.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Donation.Api.Controllers;

[ServiceFilter(typeof(ValidateModelFilter))]
[ApiController]
[Route("[controller]")]
public class WebhookController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public WebhookController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    /// Flitt will POST form-data here after checkout. 
    /// We verify signature, create user & subscription on success, and return 200.
    [HttpPost("/flitt/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(CancellationToken ct)
    {
        var request = Request.HasFormContentType
                    ? Request.Form.ToDictionary(k => k.Key, v => v.Value.ToString())
                    : new Dictionary<string, string>(StringComparer.Ordinal);

        try
        {
            await _subscriptionService.HandleFlittCallbackAsync(request, ct);

            return Ok();
        }
        catch (Exception ex)
        {
            //logger.LogError(ex, "Flitt webhook failed");
            // still return 200 if you don't want Flitt to retry; 400 if you want retries on invalid cases
            return BadRequest();
        }
    }
}
