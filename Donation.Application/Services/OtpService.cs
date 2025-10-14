using Donation.Core.OTPs;

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

        public async Task<string> SendOtpEmailAsync(string email, CancellationToken ct = default)
        {
            var otp = Generate4DigitOtp();

            var emailMessage = new
            {
                To = email,
                Subject = "[SINATLE.MEDIA] email verification code",
                Body = $"Your one time password is: {otp}"
            };

            var result = await _emailSenderClient.SendAsync(emailMessage.To, emailMessage.Subject, emailMessage.Body);

            if (result)
            {
                await _otpRepository.AddAsync(new Otp(email, otp), ct);
            }

            return await Task.FromResult("1234");
        }

        public async Task<bool> VerifyAsync(string email, string code, CancellationToken ct = default)
        {
            var otp = await _otpRepository.GetByEmailAsync(email, ct);

            if (otp == null)
            {
                // otp not found
                return await Task.FromResult(false);
            }

            if(otp.Code != code && otp.Email != email && !otp.IsUsed)
            {
                // otp is invalid or used
                return await Task.FromResult(false);
            }

            otp.MarkAsUsed();
            await _otpRepository.SaveChangesAsync(ct);

            return true;
        }


        #region Private Methods

        private static string Generate4DigitOtp()
        {
            var random = new Random();
            var otp = random.Next(1000, 9999).ToString();

            return otp;
        }

        #endregion

    }
}
