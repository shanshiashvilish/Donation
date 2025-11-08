using Donation.Api.Middlewares;
using Donation.Api.Models.Common;
using Donation.Api.Models.Requests;
using Donation.Core.Enums;
using Donation.Core.OTPs;
using Donation.Core.Users;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Donation.Api.Controllers;

[ServiceFilter(typeof(ValidateModelFilter))]
[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IOtpService _otpService;

    public AuthController(IAuthService authService, IOtpService otpService)
    {
        _authService = authService;
        _otpService = otpService;
    }

    [AllowAnonymous]
    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Token()
    {
        var req = HttpContext.GetOpenIddictServerRequest();
        if (req is null)
            return BadRequest(new { error = "invalid_request", error_description = "No OpenIddict request." });

        // A) OTP login
        if (string.Equals(req.GrantType, "otp", StringComparison.Ordinal))
        {
            var principal = await _authService.LoginAsync(req);
            if (principal is null)
                return Unauthorized(BaseResponse<object>.Fail(GeneralError.InvalidCredentials));

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // B) Refresh token
        if (req.IsRefreshTokenGrantType())
        {
            if (string.IsNullOrWhiteSpace(req.RefreshToken))
            {
                return BadRequest(BaseResponse<object>.Fail(GeneralError.InvalidCredentials));
            }

            // Validate refresh token, recover principal (and authorization)
            var auth = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (!auth.Succeeded || auth.Principal is null)
                return Unauthorized(BaseResponse<object>.Fail(GeneralError.InvalidCredentials));

            // Issue a fresh pair
            return SignIn(auth.Principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return BadRequest(BaseResponse<object>.Fail(GeneralError.Unauthorized));
    }

    [AllowAnonymous]
    [HttpPost("generate-auth-otp")]
    public async Task<ActionResult<BaseResponse<object>>> GenerateAuthOtp([FromBody] GenerateAuthOtpRequest request)
    {
        var result = await _otpService.GenerateAuthOtpAsync(request.Email);

        if (result)
        {
            return Ok(BaseResponse<bool>.Ok());
        }

        return BadRequest(BaseResponse<object>.Fail(GeneralError.Unknown));
    }
}
