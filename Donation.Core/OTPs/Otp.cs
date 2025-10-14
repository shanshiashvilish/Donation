using Donation.Core.Common;

namespace Donation.Core.OTPs;

public class Otp : Entity
{
    public string Email { get; private set; } = default!;

    public int Code { get; private set; }
}
