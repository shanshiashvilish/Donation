using Donation.Core.OTPs;

namespace Donation.Application.Services
{
    public class OtpService : IOtpService
    {
        public async Task<string> Generate4DigitOTPAsync(string email, CancellationToken ct = default)
        {
            return await Task.FromResult("1234");
        }

        public async Task<bool> VerifyAsync(string email, string code, CancellationToken ct = default)
        {
            return await Task.FromResult(code == "1234");
        }
    }
}
