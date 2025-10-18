using Donation.Application.Services;
using Donation.Core.Payments;
using Donation.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Donation.Infrastructure.Configuration;

public static class ConfigurePaymentServices
{
    public static IServiceCollection AddPaymentServices(this IServiceCollection services)
    {
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentService, PaymentService>();

        return services;
    }
}
