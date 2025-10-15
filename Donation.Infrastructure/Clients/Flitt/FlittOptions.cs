
namespace Donation.Infrastructure.Clients.Flitt;

public sealed class FlittOptions
{
    public int MerchantId { get; set; }
    public string SecretKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://pay.flitt.com";
    public string ResponseUrl { get; set; } = "";
    public string ServerCallbackBase { get; set; } = "";
    public string SubscriptionCallbackBase { get; set; } = "";
    public string CheckoutEndpoint { get; set; } = "";
    public string SubscriptionEndpoint { get; set; } = "";
}