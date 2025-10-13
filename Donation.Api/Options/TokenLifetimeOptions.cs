namespace Donation.Api.Options
{
    public sealed class TokenLifetimeOptions
    {
        public int AccessTokenMinutes { get; init; }
        public int RefreshTokenHours { get; init; }
    }
}
