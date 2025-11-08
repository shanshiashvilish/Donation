using Donation.Core.Common;

namespace Donation.Core.OTPs;

public class Otp : Entity
{
    public string Email { get; private set; } = default!;

    public string Code { get; private set; } = default!;


    public Otp()
    {

    }

    public Otp(string email, string code)
    {
        Email = email;
        Code = code;
    }
}
