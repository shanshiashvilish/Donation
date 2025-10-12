using Donation.Core.Users;

namespace Donation.Infrastructure.Repositories;

public class UserRepository(AppDbContext db) : BaseRepository<User>(db), IUserRepository
{
    public virtual async Task<bool> ExistsEmailAsync(string email, CancellationToken ct = default)
    {
        var entity = await _set.FindAsync([email], ct);
        return entity != null;
    }
}
