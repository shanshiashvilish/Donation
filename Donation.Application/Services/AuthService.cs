using Donation.Core;
using Donation.Core.Enums;
using Donation.Core.OTPs;
using Donation.Core.Users;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Donation.Application.Services
{
    public sealed class AuthService(IUserRepository userRepository, IOtpRepository otpRepository, ILogger<AuthService> logger) : IAuthService
    {
        private const string DevBypassOtp = "2468"; // dev-only backdoor (kept exactly as in your code)

        public async Task<ClaimsPrincipal?> LoginAsync(OpenIddictRequest request)
        {
            var email = NormalizeEmail(ReadParam(request, "email"));
            var otp = ReadParam(request, "otp");

            EnsureEmailAndOtpPresent(email, otp);

            logger.LogInformation("Login attempt for {email}", email);

            var isOtpValid = await ValidateOtpAsync(email, otp);
            if (!isOtpValid)
            {
                logger.LogWarning("Invalid OTP for {email}", email);
                throw new AppException(GeneralError.OtpInvalid);
            }

            var user = await userRepository.GetByEmailAsync(email);
            if (user is null)
            {
                logger.LogWarning("User not found for {email}", email);
                throw new AppException(GeneralError.UserNotFound);
            }

            var principal = BuildPrincipal(user);
            logger.LogInformation("Login success for {email} (UserId: {UserId})", email, user.Id);

            return principal;
        }


        #region Private Methods

        private static string ReadParam(OpenIddictRequest req, string key) =>
            req.GetParameter(key).ToString()?.Trim() ?? string.Empty;

        private static string NormalizeEmail(string email) =>
            email.Trim().ToLowerInvariant();

        private static void EnsureEmailAndOtpPresent(string email, string otp)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
                throw new AppException(GeneralError.EmailOrOtpNull);
        }

        private async Task<bool> ValidateOtpAsync(string email, string otp)
        {
            var valid = await otpRepository.VerifyAsync(email, otp);
            // keep exact dev bypass logic
            if (!valid && otp == DevBypassOtp)
            {
                logger.LogDebug("Dev OTP bypass used for {email}", email);
                valid = true;
            }
            return valid;
        }

        private static ClaimsPrincipal BuildPrincipal(User user)
        {
            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            identity.AddClaim(Claims.Subject, user.Id.ToString(), Destinations.AccessToken);
            identity.AddClaim(Claims.Email, user.Email, Destinations.AccessToken);
            identity.AddClaim(Claims.Role, user.Role, Destinations.AccessToken);

            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(Scopes.OpenId, "api", Scopes.OfflineAccess);
            return principal;
        }

        #endregion
    }
}
