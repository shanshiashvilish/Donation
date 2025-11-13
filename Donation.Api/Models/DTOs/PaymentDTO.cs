using Donation.Core.Enums;
using Donation.Core.Payments;

namespace Donation.Api.Models.DTOs;

public sealed class PaymentDTO
{
    public Guid Id { get; set; }

    public int Amount { get; set; }

    public PaymentType Type { get; set; }

    public Currency Currency { get; set; }

    public DateTime CreatedAt { get; set; }

    public static PaymentDTO BuildFrom(Payment payment)
    {
        return new PaymentDTO
        {
            Id = payment.Id,
            Amount = payment.Amount,
            Type = payment.Type,
            Currency = payment.Currency,
            CreatedAt = payment.CreatedAt
        };
    }
}
