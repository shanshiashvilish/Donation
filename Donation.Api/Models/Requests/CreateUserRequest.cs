using Donation.Core.Users;

namespace Donation.Api.Models.Requests
{
    public class CreateUserRequest
    {
        public string Email { get; set; }
        public string Name { get; set; }
        public string Lastname { get; set; }

        public User ToEntity()
        {
            return new(Email.Trim().ToLowerInvariant(), Name.Trim(), Lastname.Trim());
        }
    }
}
