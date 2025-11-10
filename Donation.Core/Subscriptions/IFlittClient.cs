namespace Donation.Core.Subscriptions;

public interface IFlittClient
{
    Task<(string checkoutUrl, string orderId, string externalId)> SubscribeAsync(int amountMinor, string email, string name, string lastName, CancellationToken ct = default);

    Task<bool> UnsubscribeAsync(string externalId, CancellationToken ct = default);

    bool VerifySignature(IDictionary<string, string?> responseOrCallback);
}
