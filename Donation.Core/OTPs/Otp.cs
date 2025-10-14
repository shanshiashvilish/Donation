using Donation.Core.Common;

namespace Donation.Core.OTPs;

public class Otp : Entity
{
    public string Email { get; private set; } = default!;

    public string Code { get; private set; }

    public bool IsUsed { get; private set; }

    public Otp()
    {

    }

    public Otp(string email, string code)
    {
        Email = email;
        Code = code;
        IsUsed = false;
    }

    public void MarkAsUsed()
    {
        IsUsed = true;
    }
}
