using Donation.Api.Middlewares;
using Donation.Api.Models.Common;
using Donation.Api.Models.Requests;
using Donation.Core.Enums;
using Donation.Core.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

namespace Donation.Api.Controllers;

[ServiceFilter(typeof(ValidateModelFilter))]
[ApiController]
[Route("[controller]")]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var (checkoutUrl, orderId) = await _subscriptionService.SubscribeAsync(request.Amount, request.Email, request.Name, request.LastName, ct);

        return Ok(new { checkoutUrl, orderId });
    }

    [Authorize]
    [HttpPost("{subscriptionId:guid}/unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromRoute] Guid subscriptionId, CancellationToken ct)
    {
        var result = await _subscriptionService.UnsubscribeAsync(subscriptionId, ct);

        return Ok(new { result });
    }

    [Authorize]
    [HttpPost("{subscriptionId:guid}/edit")]
    public async Task<IActionResult> Edit([FromRoute] Guid subscriptionId, [FromBody] EditSubscriptionRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var sub = User.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;

        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized(BaseResponse<object>.Fail(GeneralError.Unauthorized));

        var (checkoutUrl, newOrderId) = await _subscriptionService.EditSubscriptionAsync(userId, subscriptionId, request.NewAmount, ct);

        return Ok(new { checkoutUrl, orderId = newOrderId });
    }
}
