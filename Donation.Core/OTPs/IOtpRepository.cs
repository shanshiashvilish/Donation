using Donation.Core.Common;

namespace Donation.Core.OTPs;

public interface IOtpRepository : IBaseRepository<Otp>
{
    Task<Otp?> GetByEmailAsync(string email, CancellationToken ct = default);
}
