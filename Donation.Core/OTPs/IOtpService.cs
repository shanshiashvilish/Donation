namespace Donation.Core.OTPs;

public interface IOtpService
{
    Task<bool> SendOtpEmailAsync(string email, CancellationToken ct = default);
}
