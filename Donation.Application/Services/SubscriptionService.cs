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

        if (string.IsNullOrEmpty(checkoutUrl))
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
        foreach (var (k, v) in request) callback[k] = v;

        if (!_flittClient.VerifySignature(callback))
            throw new AppException(GeneralError.FlittSignatureInvalid);

        callback.TryGetValue("order_id", out var orderId);
        callback.TryGetValue("payment_id", out var paymentId);
        callback.TryGetValue("order_status", out var orderStatus);
        callback.TryGetValue("response_status", out var responseStatus);
        callback.TryGetValue("amount", out var amountMinorStr);
        callback.TryGetValue("currency", out var currencyStr);
        callback.TryGetValue("next_payment_date", out var nextPaymentDateStr);
        if (string.IsNullOrWhiteSpace(nextPaymentDateStr))
            callback.TryGetValue("next_billing_date", out nextPaymentDateStr);

        if (string.IsNullOrWhiteSpace(orderId))
            throw new AppException(GeneralError.MissingParameter);

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

        var subscription = await _subscriptionRepository.Query()
            .FirstOrDefaultAsync(s => s.ExternalId == orderId, ct);

        if (subscription is null && !string.IsNullOrWhiteSpace(paymentId))
        {
            subscription = await _subscriptionRepository.Query()
                .FirstOrDefaultAsync(s => s.ExternalId == paymentId, ct);
        }

        if (!paymentSucceeded)
        {
            if (subscription is not null)
            {
                var failureStatus = paymentPending ? SubscriptionStatus.Incomplete : SubscriptionStatus.PastDue;
                subscription.UpdateStatus(failureStatus, null);
                await _subscriptionRepository.SaveChangesAsync(ct);
            }
            return;
        }

        _ = int.TryParse(amountMinorStr, out var amountMinor);
        var currency = Currency.GEL; // if you add more, map currencyStr to your enum here
        var amountMajor = amountMinor / 100m;

        // 7) If subscription already exists (retry/duplicate), just activate/update & record payment
        //if (subscription is not null)
        //{
        //    subscription.Activate();
        //    UpdateNextBilling(subscription, nextPaymentDateStr);
        //    await _subscriptionRepository.SaveChangesAsync(ct);

        //    await _paymentService.CreateAsync(
        //        amountMinor,
        //        subscription.,
        //        PaymentType.Subscription,
        //        currency,
        //        subscription.Id,
        //        ct: ct);

        //    return;
        //}

        if (!callback.TryGetValue("merchant_data", out var merchantDataRaw) ||
            !TryDecodeMerchantData(merchantDataRaw, out var md))
        {
            // We can’t safely create user without our own payload
            throw new AppException(GeneralError.MissingParameter);
        }

        var email = md.Email;
        var firstname = md.Name ?? string.Empty;
        var lastname = md.LastName ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email))
            throw new AppException(GeneralError.MissingParameter);

        // 9) Create or fetch user (your checkout blocks existing users, but keep this safe)
        var user = await _userRepository.GetByEmailAsync(email, ct);
        if (user is null)
        {
            user = new User(email, firstname, lastname);
            await _userRepository.AddAsync(user, ct);
            await _userRepository.SaveChangesAsync(ct);
        }

        var created = new Subscription(user.Id, amountMajor, currency, orderId!);
        created.Activate();
        UpdateNextBilling(created, nextPaymentDateStr);

        await _subscriptionRepository.AddAsync(created, ct);
        await _subscriptionRepository.SaveChangesAsync(ct);

        await _paymentService.CreateAsync(
            amountMinor,
            user.Email,
            PaymentType.Subscription,
            currency,
            created.Id,
            ct: ct);


        static bool TryDecodeMerchantData(string? b64, out MerchantData dto)
        {
            dto = default!;
            if (string.IsNullOrWhiteSpace(b64))
                return false;

            try
            {
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64));
                dto = System.Text.Json.JsonSerializer.Deserialize<MerchantData>(json, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? default!;
                return dto is not null && !string.IsNullOrWhiteSpace(dto.Email);
            }
            catch
            {
                return false;
            }
        }
    }

    sealed class MerchantData
    {
        public string OrderId { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string? Name { get; set; }
        public string? LastName { get; set; }
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
