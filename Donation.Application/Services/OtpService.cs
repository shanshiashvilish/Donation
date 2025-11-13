using Donation.Core;
using Donation.Core.Enums;
using Donation.Core.OTPs;
using Donation.Core.Users;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Donation.Application.Services
{
    public sealed class OtpService(IOtpRepository otpRepository, IUserRepository userRepository, ISendGridClient sendGridClient, ILogger<OtpService> logger) : IOtpService
    {
        private const string Subject = "[SINATLE.MEDIA] email verification code";

        public async Task<bool> GenerateAuthOtpAsync(string email)
        {
            var normalized = NormalizeEmail(email);
            logger.LogInformation("Generating OTP for {email}", normalized);

            var exists = await userRepository.ExistsEmailAsync(normalized);
            if (!exists)
            {
                logger.LogWarning("User not found for {email}", normalized);
                throw new AppException(GeneralError.UserNotFound);
            }

            var hash = await SendOtpEmailAsync(normalized);
            if (string.IsNullOrEmpty(hash))
            {
                logger.LogError("OTP email failed to send for {email}", normalized);
                throw new AppException(GeneralError.OtpNotSent);
            }

            logger.LogInformation("OTP generated & stored for {email}", normalized);
            return true;
        }

        public async Task<string> SendOtpEmailAsync(string email, CancellationToken ct = default)
        {
            var normalized = NormalizeEmail(email);

            var otp = GenerateCode();
            var body = BuildEmailBody(otp);

            logger.LogDebug("Sending OTP email to {email}", normalized);
            await sendGridClient.SendAsync(normalized, Subject, body, ct: ct);

            var otpHash = Hash(otp);

            await otpRepository.ClearHistoryByEmailAsync(normalized, ct);
            await otpRepository.AddAsync(new Otp(normalized, otpHash), ct);
            await otpRepository.SaveChangesAsync(ct);

            logger.LogDebug("OTP persisted (hash) for {email}", normalized);
            return otpHash;
        }


        #region Private Methods

        private static string NormalizeEmail(string email) =>
            (email ?? string.Empty).Trim().ToLowerInvariant();

        private static string GenerateCode()
        {
            // 4 digit
            var code = RandomNumberGenerator.GetInt32(1000, 9999);
            return code.ToString();
        }

        private static string BuildEmailBody(string otp) =>
            $@"Your one time password is: ""{otp}""";

        private static string Hash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }

        #endregion
    }
}
