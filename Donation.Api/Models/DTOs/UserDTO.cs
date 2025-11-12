using Donation.Core.Enums;
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

        public SubscriptionDTO Subscription { get; set; } = default!;

        public List<PaymentDTO> Payments { get; set; } = [];


        public static UserDTO BuildFrom(User user)
        {
            return new UserDTO
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                Lastname = user.Lastname,
                Role = user.Role,
                CreatedAt = user.CreatedAt,
                Subscription = user.Subscriptions?.Where(s => s.Status == SubscriptionStatus.Active).Select(SubscriptionDTO.BuildFrom).FirstOrDefault()!,
                Payments = user.Payments?.Select(PaymentDTO.BuildFrom).ToList() ?? []
            };
        }
    }
}
