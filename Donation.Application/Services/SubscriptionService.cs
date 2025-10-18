using Donation.Core.Enums;
using Donation.Core.Subscriptions;
using Donation.Core.Users;
using Microsoft.EntityFrameworkCore;

namespace Donation.Application.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IUserRepository _userRepository;
        private readonly IFlittClient _flittClient;

        public SubscriptionService(ISubscriptionRepository subscriptionRepository, IUserRepository userRepository, IFlittClient flittClient)
        {
            _subscriptionRepository = subscriptionRepository;
            _userRepository = userRepository;
            _flittClient = flittClient;
        }

        public async Task<(string checkoutUrl, string orderId)> SubscribeAsync(Guid userId, decimal amount, Currency currency,
                                                                               string description, CancellationToken ct = default)
        {
            // Is this needed???

            //var userExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, ct);
            //if (!userExists) throw new KeyNotFoundException("User not found.");

            var amountMinor = (int)Math.Round(amount * 100m);

            var (checkoutUrl, orderId) = await _flittClient.SubscribeAsync(amountMinor, Currency.GEL.ToString().ToLowerInvariant(), description, ct);

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

        public async Task HandleFlittCallbackAsync(IDictionary<string, string> request, CancellationToken ct = default)
        {
            // 1) Verify signature
            //if (!FlittWebhookSignature.Verify(_opts.SecretKey, form))
            //    throw new InvalidOperationException("Invalid Flitt signature.");

            // 2) Extract fields
            request.TryGetValue("response_status", out var responseStatus);
            request.TryGetValue("order_status", out var orderStatus);
            request.TryGetValue("order_id", out var orderId);
            request.TryGetValue("payment_id", out var paymentId); // unique per charge (may be empty)
            request.TryGetValue("sender_email", out var email);
            request.TryGetValue("sender_name", out var firstname);
            request.TryGetValue("sender_lastname", out var lastname);
            request.TryGetValue("currency", out var currencyStr); // fallback
            request.TryGetValue("amount", out var amountMinorStr);

            var approved = string.Equals(responseStatus, "success", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(orderStatus, "approved", StringComparison.OrdinalIgnoreCase);

            // 3) Only process approved payments
            if (!approved)
            {
                // You can log/record failed attempt here if desired
                return;
            }

            // Parse amount/currency
            _ = int.TryParse(amountMinorStr, out var amountMinor);
            var amountMajor = amountMinor / 100m;

            if (!Enum.TryParse<Currency>(currencyStr, true, out var currency))
                currency = Currency.GEL;

            // 4) Ensure user (unregistered-user flow)
            var user = await _userRepository.GetByEmailAsync(email, default);
            {
                user = new User(email, firstname, lastname);

                await _userRepository.AddAsync(user, ct);
                await _userRepository.SaveChangesAsync(ct);
            }

            // 5) Idempotent subscription create/activate
            // Prefer correlating on orderId (your own generated id for this subscription intent).
            var sub = await _subscriptionRepository.Query()
                .FirstOrDefaultAsync(s => s.ExternalId == orderId || s.ExternalId == paymentId || s.ExternalId == orderId, ct);

            if (sub is null)
            {
                // First successful payment for this order: create subscription
                sub = new Subscription(user.Id, amountMajor, currency, !string.IsNullOrEmpty(paymentId) ? paymentId : orderId);

                await _subscriptionRepository.AddAsync(sub, ct);
                await _subscriptionRepository.SaveChangesAsync(ct);
            }
            else
            {
                // Make sure it's active
                if (sub.Status != SubscriptionStatus.Active)
                {
                    sub.Activate();

                    await _subscriptionRepository.SaveChangesAsync(ct);
                }

                // If ExternalId wasn't set previously, prefer filling it now
                if (string.IsNullOrEmpty(sub.ExternalId) && !string.IsNullOrEmpty(paymentId))
                {
                    sub.SetExternalId(paymentId);
                    await _subscriptionRepository.SaveChangesAsync(ct);
                }
            }

            // (Optional) If you track payments in a separate table, insert a row here using paymentId.
        }
    }
}
