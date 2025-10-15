using Donation.Core.OTPs;
using System.Security.Cryptography;
using System.Text;

namespace Donation.Application.Services
{
    public class OtpService : IOtpService
    {
        private readonly IOtpRepository _otpRepository;
        private readonly IEmailSenderClient _emailSenderClient;

        public OtpService(IOtpRepository otpRepository, IEmailSenderClient emailSenderClient)
        {
            _otpRepository = otpRepository;
            _emailSenderClient = emailSenderClient;
        }

        public async Task<bool> SendOtpEmailAsync(string email, CancellationToken ct = default)
        {
            var otp = RandomNumberGenerator.GetInt32(1000, 9999).ToString();

            var emailMessage = new
            {
                To = email,
                Subject = "[SINATLE.MEDIA] email verification code",
                Body = $@"Your one time password is: ""{otp}"""
            };

            var sendMail = await _emailSenderClient.SendAsync(emailMessage.To, emailMessage.Subject, emailMessage.Body);

            if (!sendMail)
            {
                // failed to send email
                return false;
            }

            var otpHash = Hash(email);
            await _otpRepository.ClearHistoryByEmailAsync(email, ct);
            await _otpRepository.AddAsync(new Otp(otpHash, otp), ct);
            await _otpRepository.SaveChangesAsync(ct);

            return true;
        }

        #region Private Methods

        private static string Hash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }

        private static bool CompareHashes(string a, string b)
        {
            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);
            return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }

        #endregion

    }
}
