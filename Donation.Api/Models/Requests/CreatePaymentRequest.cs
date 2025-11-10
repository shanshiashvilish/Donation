using System.ComponentModel.DataAnnotations;

namespace Donation.Api.Models.Requests;

public class CreatePaymentRequest
{
    [Required, Range(1, 999999)]
    public int Amount { get; set; }

    public string? Email { get; set; }
}
