using Donation.Core.Enums;
using Donation.Core.Subscriptions;

namespace Donation.Api.Models.DTOs;

public sealed class SubscriptionDTO
{
    public Guid Id { get; set; }

    public decimal Amount { get; set; }

    public Currency Currency{ get; set; }

    public DateTime? NextBillingAt { get; set; }

    public DateTime CreatedAt { get; set; }


    public static SubscriptionDTO BuildFrom(Subscription sub)
    {
        return new SubscriptionDTO
        {
            Id = sub.Id,
            Amount = sub.Amount,
            Currency = sub.Currency,
            NextBillingAt = sub.NextBillingAt,
            CreatedAt = sub.CreatedAt
        };
    }
}
