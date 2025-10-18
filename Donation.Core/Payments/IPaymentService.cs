using Donation.Core.Enums;

namespace Donation.Core.Payments;

public interface IPaymentService
{
    Task<bool> CreateAsync(int amount, string email, PaymentType paymentType);
}
