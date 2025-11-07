
namespace Donation.Core.Enums;

public enum GeneralError
{
    Unknown = 0,

    // Common
    UnexpectedError = 1,
    Unauthorized = 2,
    Success = 10,

    // Auth / OTP
    UserNotFound = 1001,
    OtpNotSent = 1002,
    OtpInvalid = 1003,
    InvalidCredentials = 1004,
    EmailOrOtpNull = 1005,
    UserAlreadyExists = 1006,

    // Data
    MissingParameter = 4001
}
