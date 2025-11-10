using Donation.Core.Users;

namespace Donation.Api.Models.DTOs
{
    public class UserDTO
    {
        public Guid Id { get; set; }

        public string Email { get; set; } = default!;

        public string Name { get; set; } = default!;

        public string Lastname { get; set; } = default!;

        public string Role { get; set; } = default!;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public static UserDTO BuildFrom(User user)
        {
            return new UserDTO
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                Lastname = user.Lastname,
                Role = user.Role,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
