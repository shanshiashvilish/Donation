using Donation.Api.Middlewares;
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
    public async Task<IActionResult> Post([FromBody] CreatePaymentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        _ = await _paymentService.CreateAsync(request.Amount, request.Email, PaymentType.Donation);

        return Created();
    }
}
