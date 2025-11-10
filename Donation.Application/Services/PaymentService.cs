using Donation.Core.Enums;
using Donation.Core.Payments;

namespace Donation.Application.Services;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;

    public PaymentService(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<bool> CreateAsync(int amount, string email, PaymentType paymentType, Currency currency = Currency.GEL, Guid? subscriptionId = null, CancellationToken ct = default)
    {
        var payment = new Payment
        {
            Type = paymentType,
            Currency = currency,
            Amount = amount,
            Email = email,
            SubscriptionId = subscriptionId
        };

        await _paymentRepository.AddAsync(payment, ct);
        await _paymentRepository.SaveChangesAsync(ct);

        return true;
    }
}
