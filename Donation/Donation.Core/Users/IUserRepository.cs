using Donation.Core.Common;

namespace Donation.Core.Users
{
    public interface IUserRepository : IBaseRepository<User>
    {
        Task<bool> ExistsEmailAsync(string email, CancellationToken ct = default);
    }
}
