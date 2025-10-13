using Donation.Core.Users;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
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

        var principal = await authService.LoginAsync(request);

        if (principal is null)
            return Unauthorized(new { error = "invalid_grant", error_description = "Invalid email/OTP." });

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Refresh([FromHeader(Name = "Authorization")] string? authHeader = null)
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        request.GrantType ??= OpenIddictConstants.GrantTypes.RefreshToken;

        if (string.IsNullOrWhiteSpace(request.RefreshToken) && !string.IsNullOrWhiteSpace(authHeader))
        {
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                request.RefreshToken = authHeader.Substring("Bearer ".Length).Trim();
        }

        if (!request.IsRefreshTokenGrantType() || string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { error = "invalid_request", error_description = "Missing refresh token." });

        var authResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!authResult.Succeeded || authResult.Principal is null)
            return Unauthorized(new { error = "invalid_grant", error_description = "Invalid refresh token." });

        return SignIn(authResult.Principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
