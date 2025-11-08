using System.ComponentModel.DataAnnotations;

namespace Donation.Api.Models.Requests;

public class CreatePaymentRequest
{
    [Required]
    public int Amount { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; } = default!;
}
