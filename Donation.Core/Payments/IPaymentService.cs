using System;
using System.Threading;
using System.Threading.Tasks;
using Donation.Core.Enums;

namespace Donation.Core.Payments;

public interface IPaymentService
{
    Task<bool> CreateAsync(int amount, string email, PaymentType paymentType, Currency currency = Currency.GEL, Guid? subscriptionId = null, CancellationToken ct = default);
}
