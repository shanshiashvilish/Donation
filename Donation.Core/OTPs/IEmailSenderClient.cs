
namespace Donation.Core.OTPs;

public interface IEmailSenderClient
{
    Task<bool> SendAsync(string to, string subject, string body);
}
