using Donation.Core;
using Donation.Core.Enums;
using Donation.Core.OTPs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid.Helpers.Mail;

namespace Donation.Infrastructure.Clients.SendGrid;

public sealed class SendGridClient(IOptions<SendGridOptions> options, ILogger<SendGridClient> logger) : ISendGridClient
{
    private readonly SendGridOptions _opt = options.Value;

    public async Task SendAsync(string toEmail, string subject, string plainTextContent, string? htmlContent = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ArgumentNullException(nameof(toEmail));

        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentNullException(nameof(subject));

        var apiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("SENDGRID_API_KEY not configured.");

        var from = new EmailAddress(_opt.FromEmail, _opt.FromName);
        var to = new EmailAddress(toEmail);
        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent ?? plainTextContent);

        var client = new global::SendGrid.SendGridClient(apiKey);

        logger.LogInformation("Sending email to {toEmail}, subject={Subject}", toEmail, subject);

        var resp = await client.SendEmailAsync(msg, ct).ConfigureAwait(false);
        if (resp.IsSuccessStatusCode)
        {
            logger.LogInformation("SendGrid email sent successfully to {toEmail}", toEmail);
            return;
        }

        var body = await resp.Body.ReadAsStringAsync(ct).ConfigureAwait(false);
        logger.LogError("SendGrid send failed: {StatusCode} {Body}", resp.StatusCode, body);

        throw new AppException(GeneralError.OtpNotSent);
    }
}
