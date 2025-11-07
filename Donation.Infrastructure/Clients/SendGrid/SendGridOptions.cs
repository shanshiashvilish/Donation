
namespace Donation.Infrastructure.Clients.SendGrid;

public sealed class SendGridOptions
{
    public string FromEmail { get; set; } = default!;
    public string FromName { get; set; } = default!;
}
