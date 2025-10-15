namespace Donation.Core.Subscriptions;

public interface IFlittClient
{
    Task<(string checkoutUrl, string orderId)> SubscribeAsync(int amountMinor, string currency, string orderDesc, CancellationToken ct = default);

    Task<bool> UnsubscribeAsync(string externalId, CancellationToken ct = default);

    bool VerifySignature(IDictionary<string, string?> responseOrCallback);
}
