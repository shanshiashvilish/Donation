using Donation.Core.Enums;
using Donation.Core.Subscriptions;

namespace Donation.Application.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IFlittClient _flittClient;

        public SubscriptionService(ISubscriptionRepository subscriptionRepository, IFlittClient flittClient)
        {
            _subscriptionRepository = subscriptionRepository;
            _flittClient = flittClient;
        }

        public async Task<(string checkoutUrl, string orderId)> SubscribeAsync(Guid userId, decimal amount, Currency currency,
                                                                               string description, CancellationToken ct = default)
        {
            // Is this needed???

            //var userExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, ct);
            //if (!userExists) throw new KeyNotFoundException("User not found.");

            var amountMinor = (int)Math.Round(amount * 100m);

            var (checkoutUrl, orderId) = await _flittClient.SubscribeAsync(amountMinor, currency.ToString(), description, ct);

            // NO DB write here — you’ll create Subscription after webhook approval.

            return (checkoutUrl, orderId);
        }

        public async Task<(string checkoutUrl, string newOrderId)> EditSubscriptionAsync(Guid subscriptionId, decimal newAmount, Currency currency,
                                                                                        string newDescription, CancellationToken ct = default)
        {
            var sub = await _subscriptionRepository.GetByIdAsync(subscriptionId, ct)
                      ?? throw new KeyNotFoundException("Subscription not found.");

            // Cancel current first
            await UnsubscribeAsync(subscriptionId, ct);

            // Start a new one (lets user enter new card/amount in hosted checkout)
            var (checkoutUrl, newOrderId) = await SubscribeAsync(sub.UserId, newAmount, currency, newDescription, ct);

            return (checkoutUrl, newOrderId);
        }

        public async Task<bool> UnsubscribeAsync(Guid subscriptionId, CancellationToken ct = default)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, ct)
                      ?? throw new KeyNotFoundException("Subscription not found.");

            if (string.IsNullOrWhiteSpace(subscription.ExternalId))
                throw new InvalidOperationException("Subscription external id is missing.");

            var flittResult = await _flittClient.UnsubscribeAsync(subscription.ExternalId, ct);

            if (flittResult)
            {
                subscription.Cancel();
                await _subscriptionRepository.SaveChangesAsync(ct);
            }

            return flittResult;
        }
    }
}
