using Donation.Core.Enums;

namespace Donation.Core.Subscriptions;

public interface ISubscriptionService
{
    Task<(string checkoutUrl, string orderId)> SubscribeAsync(decimal amount, string email, string name, string lastName, CancellationToken ct = default);

    Task<(string checkoutUrl, string newOrderId)> EditSubscriptionAsync(Guid userId, Guid subscriptionId, decimal newAmount, CancellationToken ct = default);

    Task HandleFlittCallbackAsync(IDictionary<string, string> request, CancellationToken ct = default);

    Task<bool> UnsubscribeAsync(Guid subscriptionId, CancellationToken ct = default);
}
