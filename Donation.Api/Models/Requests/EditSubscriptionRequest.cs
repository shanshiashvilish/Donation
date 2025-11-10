using System.ComponentModel.DataAnnotations;

namespace Donation.Api.Models.Requests;

public sealed class EditSubscriptionRequest
{
    [Required, Range(1, 999999)]
    public int NewAmount { get; set; }
}
