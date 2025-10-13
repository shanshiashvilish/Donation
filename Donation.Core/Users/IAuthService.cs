using System.Security.Claims;

namespace Donation.Core.Users
{
    public interface IAuthService
    {
        Task<ClaimsPrincipal?> LoginAsync(string email, string otp);
    }
}
