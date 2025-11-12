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
    private readonly IPaymentRepository _paymentRepository;

    public SubscriptionService(ISubscriptionRepository subscriptionRepository, IUserRepository userRepository, IFlittClient flittClient, IPaymentRepository paymentRepository)
    {
        _subscriptionRepository = subscriptionRepository;
        _userRepository = userRepository;
        _flittClient = flittClient;
        _paymentRepository = paymentRepository;
    }

    public async Task<string> SubscribeAsync(int amountMinor, string email, string name, string lastName, Guid? ignoreSubscriptionId = null, CancellationToken ct = default)
    {
        var existingUser = await _userRepository.GetByEmailAsync(email, true, ct);

        if (existingUser is not null)
        {
            var hasOtherLiveSub = await _subscriptionRepository.Query().AsNoTracking()
                .AnyAsync(
                    s => s.UserId == existingUser.Id
                         && (ignoreSubscriptionId == null || s.Id != ignoreSubscriptionId.Value)
                         && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Incomplete),
                    ct);

            if (hasOtherLiveSub)
                throw new AppException(GeneralError.UserAlreadyExists);
        }

        var (checkoutUrl, orderId, externalId) = await _flittClient.SubscribeAsync(amountMinor, email, name, lastName, ct);

        if (string.IsNullOrEmpty(checkoutUrl))
            throw new AppException(GeneralError.UnableToGenerateSubscriptionCheckoutUrl);

        return checkoutUrl;
    }

    public async Task<string> EditSubscriptionAsync(Guid userId, Guid subscriptionId, int newAmountMinor, CancellationToken ct = default)
    {
        var sub = await _subscriptionRepository.GetByIdAsync(subscriptionId, ct)
                  ?? throw new AppException(GeneralError.SubscriptionNotFound);

        var user = await _userRepository.GetByIdAsync(userId, ct)
                   ?? throw new AppException(GeneralError.UserNotFound);

        await UnsubscribeAsync(subscriptionId, userId, ct);

        var checkoutUrl = await SubscribeAsync(newAmountMinor, user.Email, user.Name, user.Lastname, ignoreSubscriptionId: subscriptionId, ct);

        return checkoutUrl;
    }

    public async Task<bool> UnsubscribeAsync(Guid subscriptionId, Guid userId, CancellationToken ct = default)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId, ct)
                  ?? throw new AppException(GeneralError.SubscriptionNotFound);

        if (subscription.UserId != userId)
        {
            throw new AppException(GeneralError.CurrentUserNotSubscriptionCreator);
        }

        var flittResult = await _flittClient.UnsubscribeAsync(subscription.ExternalId!, ct);

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

        var subscription = await _subscriptionRepository.Query().FirstOrDefaultAsync(s => s.ExternalId == orderId, ct);

        if (subscription is null && !string.IsNullOrWhiteSpace(paymentId))
        {
            subscription = await _subscriptionRepository.Query().FirstOrDefaultAsync(s => s.ExternalId == paymentId, ct);
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

        if (!callback.TryGetValue("merchant_data", out var merchantDataRaw) ||
            !TryDecodeMerchantData(merchantDataRaw, out var md))
        {
            throw new AppException(GeneralError.MissingParameter);
        }

        var email = md.Email;
        var firstname = md.Name ?? string.Empty;
        var lastname = md.LastName ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email))
            throw new AppException(GeneralError.MissingParameter);

        // Create or fetch user
        var user = await _userRepository.GetByEmailAsync(email, true, ct);
        if (user is null)
        {
            user = new User(email, firstname, lastname);
            await _userRepository.AddAsync(user, ct);
            await _userRepository.SaveChangesAsync(ct);
        }

        var created = new Subscription(user.Id, amountMinor, Currency.GEL, orderId!);
        created.Activate();
        created.SetNextBillingDate(DateTime.UtcNow.AddMonths(1));

        await _subscriptionRepository.AddAsync(created, ct);
        await _subscriptionRepository.SaveChangesAsync(ct);

        var payment = new Payment(amountMinor, user.Email, PaymentType.Subscription, userId: user.Id);
        await _paymentRepository.AddAsync(payment, ct);
        await _paymentRepository.SaveChangesAsync(ct);

        return;
    }

    #region Common

    private sealed class MerchantData
    {
        public string Email { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string LastName { get; set; } = default!;
    }

    private static bool TryDecodeMerchantData(string? b64, out MerchantData dto)
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

    #endregion
}
