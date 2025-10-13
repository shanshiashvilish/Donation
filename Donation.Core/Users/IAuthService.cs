using OpenIddict.Abstractions;
using System.Security.Claims;

namespace Donation.Core.Users
{
    public interface IAuthService
    {
        Task<ClaimsPrincipal?> LoginAsync(OpenIddictRequest request);
    }
}
