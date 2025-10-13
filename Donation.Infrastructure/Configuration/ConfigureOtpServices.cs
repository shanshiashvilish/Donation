using Donation.Application.Services;
using Donation.Core.OTPs;
using Donation.Core.Users;
using Donation.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Donation.Infrastructure.Configuration;

public static class ConfigureOtpServices
{
    public static IServiceCollection AddOtpServices(this IServiceCollection services)
    {
        services.AddScoped<IOtpRepository, OtpRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}
