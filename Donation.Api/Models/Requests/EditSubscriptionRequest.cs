using System.ComponentModel.DataAnnotations;

namespace Donation.Api.Models.Requests;

public sealed class EditSubscriptionRequest
{
    [Required, Range(0.5, 999999)]
    public decimal NewAmount { get; set; }
}
