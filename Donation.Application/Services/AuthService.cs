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

        public AuthService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<ClaimsPrincipal?> LoginAsync(string email, string otp)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
                return null;

            var user = await _userRepository.GetByEmailAsync(email.Trim().ToLowerInvariant());
            if (user is null) return null;

            // IMPORTANT: set an authentication type -> IsAuthenticated = true
            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            identity.AddClaim(Claims.Subject, user.Id.ToString(), Destinations.AccessToken);
            identity.AddClaim(Claims.Email, user.Email, Destinations.AccessToken);
            identity.AddClaim(Claims.Role, user.Role, Destinations.AccessToken);

            var principal = new ClaimsPrincipal(identity);

            principal.SetScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess, "api");

            return principal;
        }

    }
}
