using Donation.Core.Users;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Server.AspNetCore;

namespace Donation.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Login()
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null) return BadRequest();

        var email = (request.Username ?? request.GetParameter("email").ToString())?.Trim().ToLowerInvariant();
        var otp = (request.Password ?? request.GetParameter("otp").ToString())?.Trim();

        var principal = await authService.LoginAsync(email, otp);

        if (principal is null)
            return Unauthorized(new { error = "invalid_grant", error_description = "Invalid email/OTP." });

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
