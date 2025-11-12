using Donation.Core.Users;
using Microsoft.EntityFrameworkCore;

namespace Donation.Infrastructure.Repositories;

public class UserRepository(AppDbContext db) : BaseRepository<User>(db), IUserRepository
{
    public virtual async Task<bool> ExistsEmailAsync(string email, CancellationToken ct = default)
    {
        return await _set.AsNoTracking().AnyAsync(u => u.Email == email, ct);
    }

    public async Task<User> GetByEmailAsync(string email, bool includeProperties = false, CancellationToken ct = default)
    {
        if (includeProperties)
        {
            return await _set.Include(u => u.Subscriptions).Include(u => u.Payments).FirstOrDefaultAsync(u => u.Email == email, ct);
        }

        return await _set.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<User?> GetByIdAsync(Guid id, bool includeProperties = false, CancellationToken ct = default)
    {
        if (includeProperties)
        {
            return await _set.Include(u => u.Subscriptions).Include(u => u.Payments).FirstOrDefaultAsync(u => u.Id == id, ct);
        }

        return await _set.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
    }
}
