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

    [Authorize]
    [HttpPost("subscribe")]
    public async Task<IActionResult> SubscribeAsync([FromBody] CreateUserRequest user)
    {
        if (user == null)
        {
            return BadRequest();
        }


        return Ok();
    }

    [Authorize]
    [HttpPost("{id}/unsubscribe")]
    public async Task<IActionResult> UnsubscribeAsync([FromRoute] Guid id, [FromBody] CreateUserRequest user)
    {
        if (user == null)
        {
            return BadRequest();
        }


        return Ok();
    }
}
