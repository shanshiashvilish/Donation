using System.ComponentModel.DataAnnotations;

namespace Donation.Api.Models.Requests
{
    public class UpdateUserRequest
    {
        [Required(ErrorMessage = "Name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lastname is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Lastname must be between 2 and 100 characters.")]
        public string Lastname { get; set; } = string.Empty;
    }
}
