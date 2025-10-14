using Donation.Core.OTPs;
using Donation.Infrastructure.Clients.EmailSender;
using Microsoft.Extensions.DependencyInjection;

namespace Donation.Infrastructure.Configuration
{
    public static class ConfigureEmailSender
    {
        public static IServiceCollection AddEmailSenderServices(this IServiceCollection services)
        {
            services.AddScoped<IEmailSenderClient, EmailSenderClient>();

            return services;
        }
    }
}
