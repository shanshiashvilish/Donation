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

        public async Task<bool> VerifyAsync(string email, string code, CancellationToken ct = default)
        {
            if(code.Length != 4)
            {
                // otp lenght must be exactly 4 digits
                return false;
            }

            var otp = await _otpRepository.GetByEmailAsync(email, ct);

            if (otp == null)
            {
                // otp not found
                return false;
            }

            if (otp.Email != email)
            {
                // No such code was found matching the provided email
                return false;
            }
            var isotpValid = CompareHashes(otp.Code, Hash(code));

            if (!isotpValid)
            {
                // Invalid otp
                return false;
            }

            await _otpRepository.ClearHistoryByEmailAsync(email, ct);
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
