
namespace Donation.Core.Subscriptions;

public interface ISubscriptionService
{
    Task<string> SubscribeAsync(int amountMinor, string email, string name, string lastName, Guid? ignoreSubscriptionId = null, CancellationToken ct = default);

    Task<string> EditSubscriptionAsync(Guid userId, Guid subscriptionId, int newAmountMinor, CancellationToken ct = default);

    Task HandleFlittCallbackAsync(IDictionary<string, string> request, CancellationToken ct = default);

    Task<bool> UnsubscribeAsync(Guid subscriptionId, Guid userId, CancellationToken ct = default);
}