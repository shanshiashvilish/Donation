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
    public FlittRecurringOptions Recurring { get; set; } = new();
}

public sealed class FlittRecurringOptions
{
    public int Every { get; set; } = 1;

    public string Period { get; set; } = "month";

    public int? Quantity { get; set; } = 1;

    public string? StartTime { get; set; }

    public string? EndTime { get; set; }

    public string State { get; set; } = "Y";

    public string? Readonly { get; set; } = "Y";

    public string? TrialPeriod { get; set; }

    public int? TrialQuantity { get; set; }
}

