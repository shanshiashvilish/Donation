namespace Donation.Core.Subscriptions;

public interface IFlittClient
{
    Task<(string checkoutUrl, string orderId)> CreateSubscriptionCheckoutAsync(string orderId, int amountMinor, string currency,
                                                                               string orderDesc, string responseUrl, string serverCallbackUrl,
                                                                               string subscriptionCallbackUrl, CancellationToken ct = default);

    Task<(bool ok, string status)> ChangeSubscriptionStateAsync(string orderId, string action /* "stop" | "start" */, CancellationToken ct = default);

    bool VerifySignature(IDictionary<string, string?> responseOrCallback);
}
