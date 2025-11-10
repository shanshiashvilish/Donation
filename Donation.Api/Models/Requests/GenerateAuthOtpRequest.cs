using System.ComponentModel.DataAnnotations;

namespace Donation.Api.Models.Requests
{
    public class GenerateAuthOtpRequest
    {
        [Required, EmailAddress, MinLength(3)]
        public string Email { get; set; } = default!;
    }
}
