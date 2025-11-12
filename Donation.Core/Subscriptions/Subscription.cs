using Donation.Core.Common;
using Donation.Core.Enums;
using Donation.Core.Payments;
using Donation.Core.Users;

namespace Donation.Core.Subscriptions;

public class Subscription : Entity
{
    public Guid UserId { get; set; }

    public string? ExternalId { get; private set; } // Flitt subscription id

    public int Amount { get; private set; }

    public Currency Currency { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    public DateTime? NextBillingAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public User User { get; set; } = default!;

    public ICollection<Payment> Payments { get; private set; } = [];


    private Subscription()
    {

    }

    public Subscription(Guid userId, int amount, Currency currency, string externalId)
    {
        UserId = userId;
        Amount = amount;
        Currency = currency;
        ExternalId = externalId;
    }

    public void UpdateStatus(SubscriptionStatus status, DateTime? nextBillingAt)
    {
        Status = status;
        NextBillingAt = nextBillingAt;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = SubscriptionStatus.Canceled;
        NextBillingAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        Status = SubscriptionStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetNextBillingDate(DateTime nextBillingAt)
    {
        NextBillingAt = nextBillingAt;
        UpdatedAt = DateTime.UtcNow;
    }
}
