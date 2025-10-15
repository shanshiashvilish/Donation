using Donation.Api.Middlewares;
using Donation.Api.Models.Requests;
using Donation.Core.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        var (checkoutUrl, orderId) = await _subscriptionService.SubscribeAsync(request.UserId, request.Amount, request.Currency,
                                                                               request.Description, ct);

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

        var (checkoutUrl, newOrderId) = await _subscriptionService.EditSubscriptionAsync(subscriptionId, request.NewAmount, request.Currency,
                                                                                         request.Description, ct);

        return Ok(new { checkoutUrl, orderId = newOrderId });
    }
}
