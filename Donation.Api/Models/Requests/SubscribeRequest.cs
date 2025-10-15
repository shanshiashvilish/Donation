using Donation.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace Donation.Api.Models.Requests;

public sealed class SubscribeRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Range(0.5, 999999)]
    public decimal Amount { get; set; }

    [Required]
    public Currency Currency { get; set; } = Currency.GEL;

    [Required]
    public string Description { get; set; } = string.Empty;
}
