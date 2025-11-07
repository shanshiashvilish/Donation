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
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<BaseResponse<object>>> Post([FromBody] CreatePaymentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        _ = await _paymentService.CreateAsync(request.Amount, request.Email, PaymentType.OneTime);

        return Ok(BaseResponse<object>.Ok());
    }
}
