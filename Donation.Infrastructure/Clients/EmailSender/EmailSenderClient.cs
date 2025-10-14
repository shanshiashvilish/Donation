using Donation.Core.OTPs;

namespace Donation.Infrastructure.Clients.EmailSender;

public class EmailSenderClient : IEmailSenderClient
{
    public Task<bool> SendAsync(string to, string subject, string body)
    {
        return Task.FromResult(true);
    }
}