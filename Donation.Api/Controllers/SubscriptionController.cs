using Donation.Api.Middlewares;
using Donation.Api.Models.Common;
using Donation.Api.Models.DTOs;
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

    [AllowAnonymous]
    [HttpPost("subscribe")]
    public async Task<ActionResult<BaseResponse<CheckoutUrlDTO>>> Subscribe([FromBody] SubscribeRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var checkoutUrl = await _subscriptionService.SubscribeAsync(request.Amount, request.Email, request.Name, request.LastName, ct: ct);

        var result = new CheckoutUrlDTO
        {
            CheckoutUrl = checkoutUrl
        };

        return Ok(BaseResponse<CheckoutUrlDTO>.Ok(result));
    }

    [Authorize]
    [HttpPost("{subscriptionId:guid}/unsubscribe")]
    public async Task<ActionResult<BaseResponse<bool>>> Unsubscribe([FromRoute] Guid subscriptionId, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var sub = User.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;

        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized(BaseResponse<object>.Fail(GeneralError.Unauthorized));

        await _subscriptionService.UnsubscribeAsync(subscriptionId, userId, ct);

        return Ok(BaseResponse<bool>.Ok(true));
    }

    [Authorize]
    [HttpPost("{subscriptionId:guid}/edit")]
    public async Task<ActionResult<BaseResponse<CheckoutUrlDTO>>> Edit([FromRoute] Guid subscriptionId, [FromBody] EditSubscriptionRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var sub = User.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;

        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized(BaseResponse<object>.Fail(GeneralError.Unauthorized));

        var checkoutUrl = await _subscriptionService.EditSubscriptionAsync(userId, subscriptionId, request.NewAmount, ct);

        var result = new CheckoutUrlDTO
        {
            CheckoutUrl = checkoutUrl
        };

        return Ok(BaseResponse<CheckoutUrlDTO>.Ok(result));
    }
}
