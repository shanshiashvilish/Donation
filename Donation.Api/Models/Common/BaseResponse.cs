using Donation.Core.Enums;

namespace Donation.Api.Models.Common
{
    public sealed class BaseResponse<T>
    {
        public bool Success { get; init; }
        public T? Data { get; init; }
        public List<string> Errors { get; init; } = [];

        public static BaseResponse<T> Ok(T data) => new()
        {
            Success = true,
            Data = data,
        };

        public static BaseResponse<T> Ok() => new()
        {
            Success = true,
        };

        public static BaseResponse<T> Fail(GeneralError status) => new()
        {
            Errors = Enum.GetNames<GeneralError>()
                         .Where(name => name.Equals(status.ToString(), StringComparison.OrdinalIgnoreCase))
                         .ToList(),
        };

        public static BaseResponse<T> Unknown(IEnumerable<string> errors) => new()
        {
            Success = false,
            Errors = errors.ToList()
        };
    }
}
