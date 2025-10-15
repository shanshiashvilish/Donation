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
    }
}
