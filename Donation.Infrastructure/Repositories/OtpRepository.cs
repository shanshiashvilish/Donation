using Donation.Core.OTPs;

namespace Donation.Infrastructure.Repositories
{
    public class OtpRepository(AppDbContext db) : BaseRepository<Otp>(db), IOtpRepository
    {
    }
}
