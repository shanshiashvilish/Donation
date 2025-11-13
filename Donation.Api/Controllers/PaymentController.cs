using Donation.Api.Middlewares;
using Donation.Api.Models.Common;
using Donation.Api.Models.Requests;
using Donation.Core.Enums;
using Donation.Core.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Donation.Api.Controllers;

[ServiceFilter(typeof(ValidateModelFilter))]
[ApiController]
[Route("[controller]")]
public sealed class PaymentController(IPaymentService paymentService, ILogger<PaymentController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<BaseResponse<object>>> Post([FromBody] CreatePaymentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        logger.LogInformation("One-time payment request: amount={Amount}, email={email}",
            request.Amount, request.Email ?? string.Empty);

        _ = await paymentService.CreateAsync(request.Amount, request.Email!, PaymentType.OneTime, ct: ct);

        logger.LogInformation("One-time payment persisted for {email}", request.Email ?? string.Empty);
        return Ok(BaseResponse<object>.Ok());
    }
}
