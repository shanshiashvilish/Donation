using Donation.Core.Common;
using Donation.Core.Payments;
using Donation.Core.Subscriptions;

namespace Donation.Core.Users;

public class User : Entity
{
    public string Email { get; private set; } = default!;

    public string Name { get; private set; } = default!;

    public string Lastname { get; private set; } = default!;

    public string Role { get; set; } = default!;

    public DateTime? UpdatedAt { get; private set; }

    public ICollection<Payment> Payments { get; private set; } = [];

    public ICollection<Subscription> Subscriptions { get; private set; } = [];

    private User()
    {

    }

    public User(string email, string name, string lastName)
    {
        Email = email;
        Lastname = lastName;
        Name = name;
        Lastname = lastName;
        Role = Roles.Donor;
    }

    public void Update(string name, string lastName)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(lastName))
        {
            Lastname = lastName.Trim();
        }

        UpdatedAt = DateTime.UtcNow;
    }
}
