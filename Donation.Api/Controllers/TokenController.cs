using Donation.Core.OTPs;
using Donation.Core.Users;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;

namespace Donation.Api.Controllers;

[ApiController]
public class TokenController(IOtpService otpService, IUserService users, ILogger<TokenController> logger) : ControllerBase
{
    // OpenIddict will route POST /connect/token here because of EnableTokenEndpointPassthrough()
    [HttpPost("~/connect/token")]
    public async Task<IActionResult> Exchange(CancellationToken ct)
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
            return BadRequest(new { error = "invalid_request" });

        // Custom grant: grant_type=otp
        if (!string.Equals(request.GrantType, "otp", StringComparison.Ordinal))
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var email = (request.Username ?? request.GetParameter("email").ToString())?.Trim();
        var code = (request.Password ?? request.GetParameter("otp").ToString())?.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "invalid_request", error_description = "email and otp are required" });

        // Verify OTP
        var ok = await otpService.VerifyAsync(email, code, ct);
        if (!ok)
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        // Make sure the user exists (basic example)
        // You can extend IUserService with "GetOrCreateByEmailAsync" if needed.
        var userId = Guid.NewGuid(); // TODO: fetch real user by email; create if absent.
        var role = "user";

        // Issue claims principal for tokens
        var identity = new ClaimsIdentity(authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        identity.AddClaim(OpenIddictConstants.Claims.Subject, userId.ToString());
        identity.AddClaim(OpenIddictConstants.Claims.Email, email);
        identity.AddClaim(OpenIddictConstants.Claims.Role, role);

        // Requested scopes → resources (optional)
        var principal = new ClaimsPrincipal(identity);

        principal.SetScopes(new[]
        {
            OpenIddictConstants.Scopes.OfflineAccess, // enables refresh tokens
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Profile
        });

        // You can set resources if you want audience checks:
        // principal.SetResources("donation-api");

        // Sign in → OpenIddict generates access & refresh tokens
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
