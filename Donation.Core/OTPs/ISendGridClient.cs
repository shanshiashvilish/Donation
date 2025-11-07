
namespace Donation.Core.OTPs;

public interface ISendGridClient
{
    Task SendAsync(string toEmail, string subject, string plainTextContent, string? htmlContent = null, CancellationToken ct = default);
}
