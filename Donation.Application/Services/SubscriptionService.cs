using System.Globalization;
using Donation.Core;
using Donation.Core.Enums;
using Donation.Core.Payments;
using Donation.Core.Subscriptions;
using Donation.Core.Users;
using Microsoft.EntityFrameworkCore;

namespace Donation.Application.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IFlittClient _flittClient;
    private readonly IPaymentService _paymentService;

    public SubscriptionService(
        ISubscriptionRepository subscriptionRepository,
        IUserRepository userRepository,
        IFlittClient flittClient,
        IPaymentService paymentService)
    {
        _subscriptionRepository = subscriptionRepository;
        _userRepository = userRepository;
        _flittClient = flittClient;
        _paymentService = paymentService;
    }

    public async Task<(string checkoutUrl, string orderId)> SubscribeAsync(int amountMinor, string email, string name, string lastName, CancellationToken ct = default)
    {
        var existingUser = await _userRepository.GetByEmailAsync(email, ct);

        if (existingUser is not null)
        {
            var hasActiveSubscription = await _subscriptionRepository.Query()
                .AsNoTracking()
                .AnyAsync(
                    s => s.UserId == existingUser.Id &&
                         (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Incomplete),
                    ct);

            if (hasActiveSubscription)
            {
                throw new AppException(GeneralError.UserAlreadyExists);
            }
        }

        var (checkoutUrl, orderId, externalId) = await _flittClient.SubscribeAsync(amountMinor, email, name, lastName, ct);

        if(string.IsNullOrEmpty(checkoutUrl))
        {
            throw new AppException(GeneralError.UnableToGenerateSubscriptionCheckoutUrl);
        }

        return (checkoutUrl, orderId);
    }

    public async Task<(string checkoutUrl, string newOrderId)> EditSubscriptionAsync(Guid userId, Guid subscriptionId, int newAmountMinor, CancellationToken ct = default)
    {
        var sub = await _subscriptionRepository.GetByIdAsync(subscriptionId, ct)
                  ?? throw new AppException(GeneralError.SubscriptionNotFound);

        var user = await _userRepository.GetByIdAsync(userId, ct)
                   ?? throw new AppException(GeneralError.UserNotFound);

        await UnsubscribeAsync(subscriptionId, ct);

        var (checkoutUrl, newOrderId) = await SubscribeAsync(newAmountMinor, user.Email, user.Name, user.Lastname, ct);

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
        var callback = new Dictionary<string, string?>(request.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in request)
        {
            callback[key] = value;
        }

        var verifyFlittSignature = _flittClient.VerifySignature(callback);

        if (!verifyFlittSignature)
        {
            throw new AppException(GeneralError.FlittSignatureInvalid);
        }

        callback.TryGetValue("response_status", out var responseStatus);
        callback.TryGetValue("order_status", out var orderStatus);
        callback.TryGetValue("order_id", out var orderId);
        callback.TryGetValue("payment_id", out var paymentId);
        callback.TryGetValue("sender_email", out var email);
        callback.TryGetValue("sender_name", out var firstname);
        callback.TryGetValue("sender_lastname", out var lastname);
        callback.TryGetValue("currency", out var currencyStr);
        callback.TryGetValue("amount", out var amountMinorStr);
        callback.TryGetValue("next_payment_date", out var nextPaymentDateStr);
        if (string.IsNullOrWhiteSpace(nextPaymentDateStr))
        {
            callback.TryGetValue("next_billing_date", out nextPaymentDateStr);
        }

        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new AppException(GeneralError.MissingParameter);
        }

        var orderApproved = string.Equals(orderStatus, "approved", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(orderStatus, "paid", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(orderStatus, "completed", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(orderStatus, "succeeded", StringComparison.OrdinalIgnoreCase);

        var responseApproved = string.Equals(responseStatus, "success", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(responseStatus, "approved", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(responseStatus, "completed", StringComparison.OrdinalIgnoreCase);

        var paymentSucceeded = orderApproved && responseApproved;

        var paymentPending = string.Equals(orderStatus, "pending", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(orderStatus, "processing", StringComparison.OrdinalIgnoreCase);

        if (!int.TryParse(amountMinorStr, out var amountMinor))
        {
            if (paymentSucceeded)
            {
                throw new AppException(GeneralError.MissingParameter);
            }

            amountMinor = 0;
        }

        var currency = Currency.GEL;

        var subscription = await _subscriptionRepository.Query()
            .FirstOrDefaultAsync(s => s.ExternalId == orderId, ct);

        if (subscription is null && !string.IsNullOrWhiteSpace(paymentId))
        {
            subscription = await _subscriptionRepository.Query()
                .FirstOrDefaultAsync(s => s.ExternalId == paymentId, ct);
        }

        User? user = null;

        if (!paymentSucceeded)
        {
            if (subscription is null && !string.IsNullOrWhiteSpace(email))
            {
                user = await _userRepository.GetByEmailAsync(email, ct);

                if (user is not null)
                {
                    subscription = await _subscriptionRepository.Query()
                        .FirstOrDefaultAsync(s => s.UserId == user.Id && s.Status == SubscriptionStatus.Active, ct);
                }
            }

            if (subscription is not null)
            {
                var failureStatus = paymentPending ? SubscriptionStatus.Incomplete : SubscriptionStatus.PastDue;
                subscription.UpdateStatus(failureStatus, null);
                await _subscriptionRepository.SaveChangesAsync(ct);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new AppException(GeneralError.MissingParameter);
        }

        user ??= await _userRepository.GetByEmailAsync(email, ct);

        if (user is not null && subscription is null)
        {
            subscription = await _subscriptionRepository.Query()
                .FirstOrDefaultAsync(s => s.UserId == user.Id && s.Status == SubscriptionStatus.Active, ct);
        }

        if (user is null)
        {
            user = new User(email, firstname, lastname);

            await _userRepository.AddAsync(user, ct);
            await _userRepository.SaveChangesAsync(ct);
        }

        if (subscription is null)
        {
            var amountMajor = amountMinor / 100m;
            subscription = new Subscription(user.Id, amountMajor, currency, orderId);
            subscription.Activate();
            UpdateNextBilling(subscription, nextPaymentDateStr);

            await _subscriptionRepository.AddAsync(subscription, ct);
        }
        else
        {
            if (!string.Equals(subscription.ExternalId, orderId, StringComparison.Ordinal))
            {
                subscription.SetExternalId(orderId);
            }

            subscription.Activate();
            UpdateNextBilling(subscription, nextPaymentDateStr);
        }

        await _subscriptionRepository.SaveChangesAsync(ct);

        await _paymentService.CreateAsync(amountMinor, user.Email, PaymentType.Subscription, currency, subscription.Id, ct: ct);
    }

    private static void UpdateNextBilling(Subscription subscription, string? nextPaymentDateStr)
    {
        if (string.IsNullOrWhiteSpace(nextPaymentDateStr))
        {
            return;
        }

        if (DateTime.TryParse(nextPaymentDateStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var nextBillingAt))
        {
            subscription.UpdateStatus(subscription.Status, nextBillingAt);
        }
    }
}
