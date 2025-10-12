using Donation.Application.Services;
using Donation.Core.Users;
using Donation.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Donation.Infrastructure.Configuration;

public static class ConfigureUserServices
{
    public static IServiceCollection AddUserServices(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}
