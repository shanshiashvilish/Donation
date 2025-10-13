using Donation.Core.Users;
using System.ComponentModel.DataAnnotations;

namespace Donation.Api.Models.Requests
{
    public class CreateUserRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Email format is invalid.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lastname is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Lastname must be between 2 and 100 characters.")]
        public string Lastname { get; set; } = string.Empty;

        public User ToEntity()
        {
            return new(Email.Trim().ToLowerInvariant(), Name.Trim(), Lastname.Trim());
        }
    }
}
