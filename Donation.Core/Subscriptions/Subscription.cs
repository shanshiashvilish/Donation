using Donation.Core.Common;
using Donation.Core.Enums;

namespace Donation.Core.Subscriptions;

public class Subscription : Entity
{
    public Guid UserId { get; private set; }
    public string? ExternalId { get; private set; } // Flitt subscription id
    public decimal Amount { get; private set; }
    public Currency Currency { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTime? NextBillingAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Subscription()
    {

    }

    public Subscription(Guid userId, decimal amount, Currency currency, string externalId)
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

    public void SetExternalId(string externalId)
    {
        ExternalId = externalId;
        UpdatedAt = DateTime.UtcNow;
    }
}
