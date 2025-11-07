using Donation.Core;
using Donation.Core.Enums;
using Donation.Core.OTPs;
using Microsoft.Extensions.Options;
using SendGrid.Helpers.Mail;

namespace Donation.Infrastructure.Clients.SendGrid;

public class SendGridClient : ISendGridClient
{
    private readonly SendGridOptions _options;

    public SendGridClient(IOptions<SendGridOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(string toEmail, string subject, string plainTextContent, string? htmlContent = null, CancellationToken ct = default)
    {
        var from = new EmailAddress(_options.FromEmail, _options.FromName);
        var client = new global::SendGrid.SendGridClient(Environment.GetEnvironmentVariable("SENDGRID_API_KEY"));
        var to = new EmailAddress(toEmail);

        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent ?? plainTextContent);
        var resp = await client.SendEmailAsync(msg, ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Body.ReadAsStringAsync(ct).ConfigureAwait(false);

            throw new AppException(GeneralError.OtpNotSent);
        }
    }
}
