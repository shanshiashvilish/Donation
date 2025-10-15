namespace Donation.Core.OTPs;

public interface IOtpService
{
    Task<bool> SendOtpEmailAsync(string email, CancellationToken ct = default);

    Task<bool> VerifyAsync(string email, string code, CancellationToken ct = default);
}
