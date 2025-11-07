using Donation.Core.OTPs;
using Donation.Core.Users;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Donation.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IOtpRepository _otpRepository;


        public AuthService(IUserRepository userRepository, IOtpRepository otpRepository)
        {
            _userRepository = userRepository;
            _otpRepository = otpRepository;
        }

        public async Task<ClaimsPrincipal?> LoginAsync(OpenIddictRequest request)
        {
            string email = request.GetParameter("email").ToString().Trim().ToLowerInvariant();
            string otp = request.GetParameter("otp").ToString().Trim();

            var isOtpValid = await _otpRepository.VerifyAsync(email, otp);

            // TODO: development purposes!!!!
            if (isOtpValid == false && otp == "2468")
            {
                isOtpValid = true;
            }

            if (!isOtpValid)
            {
                //TODO: Invalid OTP
                return null;
            }

            var user = await _userRepository.GetByEmailAsync(email.Trim().ToLowerInvariant());
            if (user is null) return null;

            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            identity.AddClaim(Claims.Subject, user.Id.ToString(), Destinations.AccessToken);
            identity.AddClaim(Claims.Email, user.Email, Destinations.AccessToken);
            identity.AddClaim(Claims.Role, user.Role, Destinations.AccessToken);

            var principal = new ClaimsPrincipal(identity);

            principal.SetScopes(Scopes.OpenId, "api", Scopes.OfflineAccess);

            return principal;
        }
    }
}
