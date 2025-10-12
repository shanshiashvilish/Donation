namespace Donation.Core.OTPs;

public interface IOtpService
{
    Task<string> Generate4DigitOTPAsync(string email, CancellationToken ct = default);

    Task<bool> VerifyAsync(string email, string code, CancellationToken ct = default);
}
