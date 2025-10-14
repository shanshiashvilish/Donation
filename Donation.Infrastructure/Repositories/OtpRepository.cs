using Donation.Core.OTPs;
using Microsoft.EntityFrameworkCore;

namespace Donation.Infrastructure.Repositories
{
    public class OtpRepository(AppDbContext db) : BaseRepository<Otp>(db), IOtpRepository
    {
        public async Task<Otp?> GetByEmailAsync(string email, CancellationToken ct = default)
        {
            var otp = await _set.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

            return otp;
        }
    }
}
