using Donation.Core.Common;

namespace Donation.Core.Users
{
    public interface IUserRepository : IBaseRepository<User>
    {
        Task<bool> ExistsEmailAsync(string email, CancellationToken ct = default);

        Task<User> GetByEmailAsync(string email, bool includeProperties = false, CancellationToken ct = default);

        Task<User?> GetByIdAsync(Guid id, bool includeProperties = false, CancellationToken ct = default);
    }
}
