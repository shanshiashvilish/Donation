using Donation.Core.Common;

namespace Donation.Core.Users;

public class User : Entity
{
    public string Email { get; private set; }
    public string Name { get; private set; }
    public string Lastname { get; private set; }
    public string Role { get; set; }
    public DateTime? UpdatedAt { get; private set; }

    private User()
    {

    }

    public User(string email, string name, string lastName)
    {
        Email = email;
        Lastname = lastName;
        Name = name;
        Lastname = lastName;
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
