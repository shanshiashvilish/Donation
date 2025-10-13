using Donation.Application.Services;
using Donation.Core.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Donation.Infrastructure.Configuration
{
    public static class ConfigureAuthServices
    {
        public static IServiceCollection AddAuthServices(this IServiceCollection services)
        {
            services.AddScoped<IAuthService, AuthService>();

            return services;
        }
    }
}
