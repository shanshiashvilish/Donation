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
public sealed class AuthController(IAuthService authService, IOtpService otpService, ILogger<AuthController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Token()
    {
        var req = HttpContext.GetOpenIddictServerRequest();
        if (req is null)
        {
            logger.LogWarning("Token endpoint hit with no OpenIddict request.");
            return BadRequest(new { error = "invalid_request", error_description = "No OpenIddict request." });
        }

        if (string.Equals(req.GrantType, "otp", StringComparison.Ordinal))
            return await HandleOtpGrantAsync(req);

        if (req.IsRefreshTokenGrantType())
            return await HandleRefreshTokenGrantAsync();

        logger.LogWarning("Unsupported grant type: {GrantType}", req.GrantType);
        return BadRequest(BaseResponse<object>.Fail(GeneralError.Unauthorized));
    }

    [AllowAnonymous]
    [HttpPost("generate-auth-otp")]
    public async Task<ActionResult<BaseResponse<object>>> GenerateAuthOtp([FromBody] GenerateAuthOtpRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        logger.LogInformation("GenerateAuthOtp requested for {email}", request.Email);

        var ok = await otpService.GenerateAuthOtpAsync(request.Email);
        if (ok)
        {
            logger.LogInformation("OTP generated and sent to {email}", request.Email);
            return Ok(BaseResponse<bool>.Ok(true));
        }

        logger.LogError("Unknown failure while generating OTP for {email}", request.Email);
        return BadRequest(BaseResponse<object>.Fail(GeneralError.Unknown));
    }


    #region Private Methods

    private async Task<IActionResult> HandleOtpGrantAsync(OpenIddictRequest req)
    {
        logger.LogInformation("OTP grant requested.");

        var principal = await authService.LoginAsync(req);
        if (principal is null)
        {
            logger.LogWarning("OTP login failed: null principal.");
            return Unauthorized(BaseResponse<object>.Fail(GeneralError.InvalidCredentials));
        }

        logger.LogInformation("OTP login success.");
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> HandleRefreshTokenGrantAsync()
    {
        logger.LogInformation("Refresh token grant requested.");

        var refreshTokenMissing = string.IsNullOrWhiteSpace(HttpContext.GetOpenIddictServerRequest()?.RefreshToken);
        if (refreshTokenMissing)
        {
            logger.LogWarning("Refresh token missing.");
            return BadRequest(BaseResponse<object>.Fail(GeneralError.InvalidCredentials));
        }

        var auth = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!auth.Succeeded || auth.Principal is null)
        {
            logger.LogWarning("Refresh token auth failed.");
            return Unauthorized(BaseResponse<object>.Fail(GeneralError.InvalidCredentials));
        }

        logger.LogInformation("Refresh token success.");
        return SignIn(auth.Principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    #endregion
}
