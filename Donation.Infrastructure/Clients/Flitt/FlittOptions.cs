
namespace Donation.Infrastructure.Clients.Flitt;

public sealed class FlittOptions
{
    public int MerchantId { get; set; }
    public string SecretKey { get; set; } = default!;
    public string BaseUrl { get; set; } = default!;
    public string ResponseUrl { get; set; } = default!;
    public string ServerCallbackUrl { get; set; } = default!;
    public string SubscriptionCallbackUrl { get; set; } = default!;
    public string CheckoutEndpoint { get; set; } = default!;
    public string SubscriptionEndpoint { get; set; } = default!;
}