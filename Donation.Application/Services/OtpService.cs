using Donation.Core.OTPs;
using Donation.Core.Users;
using System.Security.Cryptography;
using System.Text;

namespace Donation.Application.Services
{
    public class OtpService : IOtpService
    {
        private readonly IOtpRepository _otpRepository;
        private readonly IUserRepository _userRepository;
        private readonly ISendGridClient _sendGridClient;

        public OtpService(IOtpRepository otpRepository, IUserRepository userRepository, ISendGridClient sendGridClient)
        {
            _otpRepository = otpRepository;
            _userRepository = userRepository;
            _sendGridClient = sendGridClient;
        }

        public async Task<bool> GenerateAuthOtpAsync(string email)
        {
            var exists = await _userRepository.ExistsEmailAsync(email);

            if (!exists)
            {
                // TODO: user with this email doesnt exist
                return false;
            }

            var sendEmail = await SendOtpEmailAsync(email);

            if (string.IsNullOrEmpty(sendEmail))
            {
                // TODO: unable to send otp;
                return false;
            }

            return true;
        }

        public async Task<string> SendOtpEmailAsync(string email, CancellationToken ct = default)
        {
            var otp = RandomNumberGenerator.GetInt32(1000, 9999).ToString();

            var emailMessage = new
            {
                To = email,
                Subject = "[SINATLE.MEDIA] email verification code",
                Body = $@"Your one time password is: ""{otp}"""
            };

            await _sendGridClient.SendAsync(emailMessage.To, emailMessage.Subject, emailMessage.Body, ct: ct);

            var otpHash = Hash(otp);
            await _otpRepository.ClearHistoryByEmailAsync(email, ct);
            await _otpRepository.AddAsync(new Otp(email, otpHash), ct);
            await _otpRepository.SaveChangesAsync(ct);

            return otpHash;
        }

        #region Private Methods

        private static string Hash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }

        #endregion

    }
}
