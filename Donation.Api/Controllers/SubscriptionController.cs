using Donation.Api.Extensions;
using Donation.Api.Middlewares;
using Donation.Api.Models.Common;
using Donation.Api.Models.DTOs;
using Donation.Api.Models.Requests;
using Donation.Core.Enums;
using Donation.Core.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Donation.Api.Controllers;

[ServiceFilter(typeof(ValidateModelFilter))]
[ApiController]
[Route("[controller]")]
public sealed class SubscriptionController(ISubscriptionService subscriptionService, ILogger<SubscriptionController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("subscribe")]
    public async Task<ActionResult<BaseResponse<CheckoutUrlDTO>>> Subscribe([FromBody] SubscribeRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        logger.LogInformation("Subscribe requested: amount={Amount}, email={email}", request.Amount, request.Email);

        var checkoutUrl = await subscriptionService.SubscribeAsync(request.Amount, request.Email, request.Name, request.LastName, ct: ct);

        logger.LogInformation("Checkout URL issued for {email}", request.Email);

        return Ok(BaseResponse<CheckoutUrlDTO>.Ok(new CheckoutUrlDTO { CheckoutUrl = checkoutUrl }));
    }

    [Authorize]
    [HttpPost("{subscriptionId:guid}/unsubscribe")]
    public async Task<ActionResult<BaseResponse<bool>>> Unsubscribe([FromRoute] Guid subscriptionId, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        if (!User.TryGetUserId(out var userId))
            return Unauthorized(BaseResponse<object>.Fail(GeneralError.Unauthorized));

        logger.LogInformation("Unsubscribe requested by {UserId} for {SubscriptionId}", userId, subscriptionId);

        await subscriptionService.UnsubscribeAsync(subscriptionId, userId, ct);

        logger.LogInformation("Unsubscribe completed for {SubscriptionId}", subscriptionId);
        return Ok(BaseResponse<bool>.Ok(true));
    }

    [Authorize]
    [HttpPost("{subscriptionId:guid}/edit")]
    public async Task<ActionResult<BaseResponse<CheckoutUrlDTO>>> Edit([FromRoute] Guid subscriptionId, [FromBody] EditSubscriptionRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        if (!User.TryGetUserId(out var userId))
            return Unauthorized(BaseResponse<object>.Fail(GeneralError.Unauthorized));

        logger.LogInformation("Edit subscription requested by {UserId} for {SubscriptionId} -> newAmount={Amount}",
            userId, subscriptionId, request.NewAmount);

        var checkoutUrl = await subscriptionService.EditSubscriptionAsync(userId, subscriptionId, request.NewAmount, ct);

        logger.LogInformation("Edit subscription produced checkout URL for {SubscriptionId}", subscriptionId);
        return Ok(BaseResponse<CheckoutUrlDTO>.Ok(new CheckoutUrlDTO { CheckoutUrl = checkoutUrl }));
    }
}
