using System.Security.Claims;
using OpenIddict.Abstractions;

namespace Donation.Api.Extensions
{
    public static class UserExtensions
    {
        public static bool TryGetUserId(this ClaimsPrincipal user, out Guid userId)
        {
            userId = Guid.Empty;

            if (user is null)
                return false;

            var sub = user.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
            return Guid.TryParse(sub, out userId);
        }
    }
}
