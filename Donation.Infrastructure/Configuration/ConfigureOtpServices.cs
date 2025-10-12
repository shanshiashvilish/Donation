using Donation.Application.Services;
using Donation.Core.OTPs;
using Donation.Core.Users;
using Donation.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Donation.Infrastructure.Configuration
{
    public static class ConfigureOtpServices
    {
        public static IServiceCollection AddOtpServices(this IServiceCollection services)
        {
            services.AddScoped<IOtpRepository, OtpReposi>();
            services.AddScoped<IUserRepository, UserRepository>();

            return services;
        }
    }
}
