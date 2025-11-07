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

    public async Task<bool> CreateAsync(int amount, string email, PaymentType paymentType, Currency currency = Currency.GEL)
    {
        var payment = new Payment
        {
            Type = paymentType,
            Currency = currency,
            Amount = amount,
            Email = email
        };

        await _paymentRepository.AddAsync(payment);

        return true;
    }
}
