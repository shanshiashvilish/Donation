using Donation.Core.Common;
using Donation.Core.Enums;

namespace Donation.Core.Payments;

public class Payment : Entity
{
    public int Amount { get; set; }

    public PaymentType Type { get; set; }

    public Guid? SubscriptionId { get; set; }

    public string? Email { get; set; }
}
