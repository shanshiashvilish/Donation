using Donation.Core.Enums;

namespace Donation.Core
{
    public class AppException : Exception
    {
        public GeneralError ErrorCode { get; }

        public AppException(GeneralError errorCode, string? message = null, Exception? inner = null) : base(message ?? errorCode.ToString(), inner)
        {
            ErrorCode = errorCode;
        }
    }
}
