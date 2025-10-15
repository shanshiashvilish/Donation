
using Donation.Core.Enums;

namespace Donation.Core.Subscriptions;

public interface ISubscriptionService
{
    Task<(string checkoutUrl, string orderId)> SubscribeAsync(Guid userId, decimal amount, Currency currency,
                                                                               string description, CancellationToken ct = default);

    Task<(string checkoutUrl, string newOrderId)> EditSubscriptionAsync(Guid subscriptionId, decimal newAmount, Currency currency,
                                                                                         string newDescription, CancellationToken ct = default);

    Task<bool> UnsubscribeAsync(Guid subscriptionId, CancellationToken ct = default);
}
