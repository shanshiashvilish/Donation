using Donation.Core.Subscriptions;

namespace Donation.Infrastructure.Repositories
{
    public class SubscriptionRepository(AppDbContext db) : BaseRepository<Subscription>(db), ISubscriptionRepository
    {
    }
}
