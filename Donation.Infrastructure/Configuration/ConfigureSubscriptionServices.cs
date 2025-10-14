using Donation.Application.Services;
using Donation.Core.OTPs;
using Donation.Core.Subscriptions;
using Donation.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Donation.Infrastructure.Configuration
{
    public static class ConfigureSubscriptionServices
    {
        public static IServiceCollection AddSubscriptionrServices(this IServiceCollection services)
        {
            services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
            services.AddScoped<ISubscriptionService, SubscriptionService>();

            return services;
        }
    }
}
