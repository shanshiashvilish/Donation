using Donation.Core;
using Donation.Core.Enums;
using Donation.Core.Payments;
using Donation.Core.Subscriptions;
using Donation.Core.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Donation.Application.Services;

public sealed class SubscriptionService(ISubscriptionRepository subscriptionRepository, IUserRepository userRepository, IFlittClient flittClient,
                                        IPaymentRepository paymentRepository, ILogger<SubscriptionService> logger) : ISubscriptionService
{
    public async Task<string> SubscribeAsync(int amountMinor, string email, string name, string lastName, Guid? ignoreSubscriptionId = null, CancellationToken ct = default)
    {
        logger.LogInformation("SubscribeAsync called for {email}, amount: {Amount}",
            email, amountMinor);

        var existingUser = await userRepository.GetByEmailAsync(email, includeProperties: true, ct);
        if (existingUser is not null)
        {
            var hasLive = await HasOtherLiveSubscriptionAsync(existingUser.Id, ignoreSubscriptionId, ct);
            if (hasLive)
            {
                logger.LogWarning("User {UserId} already has a live subscription (ignoring {IgnoreId})",
                    existingUser.Id, ignoreSubscriptionId);
                throw new AppException(GeneralError.UserAlreadyExists);
            }
        }

        var (checkoutUrl, orderId, externalId) = await flittClient.SubscribeAsync(amountMinor, email, name, lastName, ct);
        logger.LogDebug("Flitt SubscribeAsync returned orderId={OrderId}, externalId={ExternalId}", orderId, externalId);

        if (string.IsNullOrEmpty(checkoutUrl))
            throw new AppException(GeneralError.UnableToGenerateSubscriptionCheckoutUrl);

        return checkoutUrl;
    }

    public async Task<string> EditSubscriptionAsync(Guid userId, Guid subscriptionId, int newAmountMinor, CancellationToken ct = default)
    {
        logger.LogInformation("Editing subscription {SubId} for user {UserId} to amount {Amount}",
            subscriptionId, userId, newAmountMinor);

        var sub = await subscriptionRepository.GetByIdAsync(subscriptionId, ct)
                  ?? throw new AppException(GeneralError.SubscriptionNotFound);

        var user = await userRepository.GetByIdAsync(userId, ct)
                   ?? throw new AppException(GeneralError.UserNotFound);

        await UnsubscribeAsync(subscriptionId, userId, ct);

        var url = await SubscribeAsync(newAmountMinor, user.Email, user.Name, user.Lastname, ignoreSubscriptionId: subscriptionId, ct);
        return url;
    }

    public async Task<bool> UnsubscribeAsync(Guid subscriptionId, Guid userId, CancellationToken ct = default)
    {
        logger.LogInformation("Unsubscribing {SubId} by {UserId}", subscriptionId, userId);

        var subscription = await subscriptionRepository.GetByIdAsync(subscriptionId, ct)
                         ?? throw new AppException(GeneralError.SubscriptionNotFound);

        if (subscription.UserId != userId)
            throw new AppException(GeneralError.CurrentUserNotSubscriptionCreator);

        var ok = await flittClient.UnsubscribeAsync(subscription.ExternalId!, ct);
        if (ok)
        {
            subscription.Cancel();
            await subscriptionRepository.SaveChangesAsync(ct);
            logger.LogInformation("Unsubscribed {SubId} successfully", subscriptionId);
        }
        else
        {
            logger.LogWarning("Flitt unsubscribe failed for {SubId}", subscriptionId);
        }

        return ok;
    }

    public async Task HandleFlittCallbackAsync(IDictionary<string, string> request, CancellationToken ct = default)
    {
        var callback = CopyCaseInsensitive(request);

        logger.LogInformation("Flitt callback received: keys={Keys}", string.Join(",", callback.Keys));

        if (!flittClient.VerifySignature(callback))
        {
            logger.LogWarning("Flitt signature invalid");
            throw new AppException(GeneralError.FlittSignatureInvalid);
        }

        callback.TryGetValue("order_id", out var orderId);
        callback.TryGetValue("payment_id", out var paymentId);
        callback.TryGetValue("order_status", out var orderStatus);
        callback.TryGetValue("response_status", out var responseStatus);
        callback.TryGetValue("amount", out var amountMinorStr);
        callback.TryGetValue("masked_card", out var maskedCard);

        if (string.IsNullOrWhiteSpace(orderId))
            throw new AppException(GeneralError.MissingParameter);

        var paymentSucceeded = IsApproved(orderStatus) && IsApproved(responseStatus);
        var paymentPending = IsPending(orderStatus);

        var subscription = await FindSubscriptionAsync(orderId!, paymentId, ct);

        if (!paymentSucceeded)
        {
            if (subscription is not null)
            {
                var failureStatus = paymentPending ? SubscriptionStatus.Incomplete : SubscriptionStatus.PastDue;
                subscription.UpdateStatus(failureStatus, nextBillingAt: null);
                await subscriptionRepository.SaveChangesAsync(ct);
                logger.LogInformation("Subscription {SubId} marked {Status}", subscription.Id, failureStatus);
            }
            return;
        }

        _ = int.TryParse(amountMinorStr, out var amountMinor);

        if (!callback.TryGetValue("merchant_data", out var merchantDataRaw) ||
            !TryDecodeMerchantData(merchantDataRaw, out var md))
        {
            logger.LogWarning("Missing/invalid merchant_data");
            throw new AppException(GeneralError.MissingParameter);
        }

        var email = md.Email;
        var firstname = md.Name ?? string.Empty;
        var lastname = md.LastName ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email))
            throw new AppException(GeneralError.MissingParameter);

        // Create or fetch user
        var user = await userRepository.GetByEmailAsync(email, includeProperties: true, ct);
        if (user is null)
        {
            user = new User(email, firstname, lastname);
            await userRepository.AddAsync(user, ct);
            await userRepository.SaveChangesAsync(ct);
            logger.LogInformation("User created from callback: {email}, Id={UserId}", email, user.Id);
        }

        var created = new Subscription(user.Id, amountMinor, Currency.GEL, orderId!, maskedCard!);
        created.Activate();
        created.SetNextBillingDate(DateTime.UtcNow.AddMonths(1));

        await subscriptionRepository.AddAsync(created, ct);
        await subscriptionRepository.SaveChangesAsync(ct);
        logger.LogInformation("Subscription created & activated for user {UserId} (SubId: {SubId})", user.Id, created.Id);

        var payment = new Payment(amountMinor, user.Email, PaymentType.Subscription, userId: user.Id);
        await paymentRepository.AddAsync(payment, ct);
        await paymentRepository.SaveChangesAsync(ct);
        logger.LogInformation("Initial subscription payment recorded for user {UserId}", user.Id);
    }



    #region Private Methods

    private async Task<bool> HasOtherLiveSubscriptionAsync(Guid userId, Guid? ignoreId, CancellationToken ct)
    {
        return await subscriptionRepository.Query().AsNoTracking().AnyAsync(
            s => s.UserId == userId
                 && (ignoreId == null || s.Id != ignoreId.Value)
                 && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Incomplete),
            ct);
    }

    private async Task<Subscription?> FindSubscriptionAsync(string orderId, string? paymentId, CancellationToken ct)
    {
        var sub = await subscriptionRepository.Query().FirstOrDefaultAsync(s => s.ExternalId == orderId, ct);
        if (sub is null && !string.IsNullOrWhiteSpace(paymentId))
        {
            sub = await subscriptionRepository.Query().FirstOrDefaultAsync(s => s.ExternalId == paymentId, ct);
        }
        return sub;
    }

    private static bool IsApproved(string? status) =>
        string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);

    private static bool IsPending(string? status) =>
        string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "processing", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string?> CopyCaseInsensitive(IDictionary<string, string> src)
    {
        var d = new Dictionary<string, string?>(src.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in src) d[k] = v;
        return d;
    }

    private sealed class MerchantData
    {
        public string Email { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string LastName { get; set; } = default!;
    }

    private static bool TryDecodeMerchantData(string? b64, out MerchantData dto)
    {
        dto = default!;
        if (string.IsNullOrWhiteSpace(b64)) return false;

        try
        {
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            dto = System.Text.Json.JsonSerializer.Deserialize<MerchantData>(
                json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? default!;
            return dto is not null && !string.IsNullOrWhiteSpace(dto.Email);
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
