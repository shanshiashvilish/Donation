using Donation.Core.Enums;

namespace Donation.Core.Payments;

public interface IPaymentService
{
    Task<bool> CreateAsync(int amount, string email, PaymentType paymentType, Currency currency = Currency.GEL, Guid? userId = null, Guid? subscriptionId = null, CancellationToken ct = default);
}
