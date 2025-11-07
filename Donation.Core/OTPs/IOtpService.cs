namespace Donation.Core.OTPs;

public interface IOtpService
{
    Task<bool> GenerateAuthOtpAsync(string email);
    Task<string> SendOtpEmailAsync(string email, CancellationToken ct = default);
}
