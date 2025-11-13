using Donation.Core.Enums;
using Donation.Core.Payments;
using Microsoft.Extensions.Logging;

namespace Donation.Application.Services;

public sealed class PaymentService(IPaymentRepository paymentRepository, ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<bool> CreateAsync(int amount, string email, PaymentType type, Currency currency = Currency.GEL, Guid? userId = null, Guid? subscriptionId = null, CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            logger.LogWarning("Skipped creating payment: invalid amount {Amount} for {EmailMasked}", amount, email);
            return false;
        }

        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        var payment = new Payment(amount, normalizedEmail, type, currency, userId, subscriptionId);

        logger.LogInformation(
            "Creating payment of {Amount}{Currency} ({Type}) for {email} [UserId: {UserId}, SubId: {SubId}]",
            amount, currency, type, normalizedEmail, userId, subscriptionId);

        await paymentRepository.AddAsync(payment, ct);
        await paymentRepository.SaveChangesAsync(ct);

        logger.LogInformation("Payment created successfully for {email}", normalizedEmail);
        return true;
    }
}
