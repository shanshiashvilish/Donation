using System.ComponentModel.DataAnnotations;

namespace Donation.Api.Models.Requests;

public sealed class SubscribeRequest
{
    [Range(0.5, 999999)]
    public decimal Amount { get; set; }

    [Required, EmailAddress, MinLength(3)]
    public string Email { get; set; } = default!;

    [Required, MinLength(2)]
    public string Name { get; set; } = default!;

    [Required, MinLength(2)]
    public string LastName { get; set; } = default!;
}
