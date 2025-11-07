using Donation.Core.OTPs;
using Donation.Infrastructure.Clients.SendGrid;
using Microsoft.Extensions.DependencyInjection;

namespace Donation.Infrastructure.Configuration
{
    public static class ConfigureSendGridClient
    {
        public static IServiceCollection AddSendGridServices(this IServiceCollection services)
        {
            services.AddScoped<ISendGridClient, SendGridClient>();

            return services;
        }
    }
}
