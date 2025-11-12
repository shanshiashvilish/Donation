using Donation.Core.Common;
using Donation.Core.Enums;
using Donation.Core.Subscriptions;
using Donation.Core.Users;

namespace Donation.Core.Payments;

public class Payment : Entity
{
    public Guid? UserId { get; set; }

    public Guid? SubscriptionId { get; set; }

    public string? Email { get; set; }

    public int Amount { get; set; }

    public PaymentType Type { get; set; }

    public Currency Currency { get; set; }

    public User? User { get; set; } = default!;

    public Subscription? Subscription { get; set; }


    public Payment()
    {

    }

    public Payment(int amount, string email, PaymentType paymentType, Currency currency = Currency.GEL, Guid? userId = null, Guid? subscriptionId = null)
    {
        Amount = amount;
        Email = email;
        Type = paymentType;
        Currency = currency;
        UserId = userId;
        SubscriptionId = subscriptionId;
    }
}
