using Donation.Core.Subscriptions;
using Donation.Infrastructure.Clients.Flitt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Donation.Infrastructure.Configuration;

public static class ConfigureFlittServices
{
    public static IServiceCollection AddFlittClientServices(this IServiceCollection services)
    {
        services.AddHttpClient<IFlittClient, FlittClient>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<FlittOptions>>().Value;
            http.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/'));
        });

        return services;
    }
}
