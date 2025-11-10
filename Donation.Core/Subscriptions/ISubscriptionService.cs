
namespace Donation.Core.Subscriptions;

public interface ISubscriptionService
{
    Task<(string checkoutUrl, string orderId)> SubscribeAsync(int amountMinor, string email, string name, string lastName, CancellationToken ct = default);

    Task<(string checkoutUrl, string newOrderId)> EditSubscriptionAsync(Guid userId, Guid subscriptionId, int newAmountMinor, CancellationToken ct = default);

    Task HandleFlittCallbackAsync(IDictionary<string, string> request, CancellationToken ct = default);

    Task<bool> UnsubscribeAsync(Guid subscriptionId, CancellationToken ct = default);
}
