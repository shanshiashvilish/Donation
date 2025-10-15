using Donation.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Donation.Api.Models.Requests;

public sealed class EditSubscriptionRequest
{
    [Range(0.5, 999999)]
    public decimal NewAmount { get; set; }

    [Required]
    public Currency Currency { get; set; } = Currency.GEL;

    [Required, MinLength(3)]
    public string Description { get; set; } = string.Empty;
}
