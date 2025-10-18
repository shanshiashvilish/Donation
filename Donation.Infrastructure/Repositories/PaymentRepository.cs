using Donation.Core.Payments;

namespace Donation.Infrastructure.Repositories;

public class PaymentRepository(AppDbContext db) : BaseRepository<Payment>(db), IPaymentRepository
{
}
