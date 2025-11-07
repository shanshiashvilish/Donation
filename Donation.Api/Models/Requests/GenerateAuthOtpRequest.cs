using System.ComponentModel.DataAnnotations;

namespace Donation.Api.Models.Requests
{
    public class GenerateAuthOtpRequest
    {
        [Required]
        [MinLength(3)]
        [EmailAddress]
        public string Email { get; set; } = default!;
    }
}
