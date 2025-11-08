using Donation.Core;
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

        public async Task<(string checkoutUrl, string orderId)> SubscribeAsync(decimal amount, string email, string name, string lastName, CancellationToken ct = default)
        {
            var emailExists = await _userRepository.ExistsEmailAsync(email, ct);
            if (emailExists)
            {
                throw new AppException(GeneralError.UserAlreadyExists);
            }

            var amountMinor = (int)Math.Round(amount * 100m);

            var (checkoutUrl, orderId) = await _flittClient.SubscribeAsync(amountMinor, email, name, lastName, ct);

            return (checkoutUrl, orderId);
        }

        public async Task<(string checkoutUrl, string newOrderId)> EditSubscriptionAsync(Guid userId, Guid subscriptionId, decimal newAmount, CancellationToken ct = default)
        {
            var sub = await _subscriptionRepository.GetByIdAsync(subscriptionId, ct)
                      ?? throw new AppException(GeneralError.SubscriptionNotFound);

            var user = await _userRepository.GetByIdAsync(userId, ct)
                       ?? throw new AppException(GeneralError.UserNotFound);

            await UnsubscribeAsync(subscriptionId, ct);

            var (checkoutUrl, newOrderId) = await SubscribeAsync(newAmount, user.Email, user.Name, user.Lastname, ct);

            return (checkoutUrl, newOrderId);
        }

        public async Task<bool> UnsubscribeAsync(Guid subscriptionId, CancellationToken ct = default)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, ct)
                      ?? throw new AppException(GeneralError.SubscriptionNotFound);

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
            var verifyFlittSignature = _flittClient.VerifySignature(request);

            if (!verifyFlittSignature)
            {
                throw new AppException(GeneralError.FlittSignatureInvalid);
            }

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

            if (!approved)
            {
                // log/record failed attempt here if desired
                return;
            }

            _ = int.TryParse(amountMinorStr, out var amountMinor);
            var amountMajor = amountMinor / 100m;

            if (!Enum.TryParse<Currency>(currencyStr, true, out var currency))
                currency = Currency.GEL;

            //  Ensure user (unregistered-user flow)
            var user = await _userRepository.GetByEmailAsync(email, default);
            {
                user = new User(email, firstname, lastname);

                await _userRepository.AddAsync(user, ct);
                await _userRepository.SaveChangesAsync(ct);
            }

            // 5) subscription create/activate
            var subscription = await _subscriptionRepository.Query()
                .FirstOrDefaultAsync(s => s.ExternalId == orderId || s.ExternalId == paymentId || s.ExternalId == orderId, ct);

            if (subscription is null)
            {
                // create subscription
                subscription = new Subscription(user.Id, amountMajor, currency, string.IsNullOrEmpty(paymentId) ? orderId : paymentId);

                await _subscriptionRepository.AddAsync(subscription, ct);
                await _subscriptionRepository.SaveChangesAsync(ct);
            }
            else
            {
                // Make sure it's active
                if (subscription.Status != SubscriptionStatus.Active)
                {
                    subscription.Activate();

                    await _subscriptionRepository.SaveChangesAsync(ct);
                }

                // If ExternalId wasn't set previously, prefer filling it now
                if (string.IsNullOrEmpty(subscription.ExternalId) && !string.IsNullOrEmpty(paymentId))
                {
                    subscription.SetExternalId(paymentId);
                    await _subscriptionRepository.SaveChangesAsync(ct);
                }
            }

            // (Optional) If you track payments in a separate table, insert a row here using paymentId.
        }
    }
}
