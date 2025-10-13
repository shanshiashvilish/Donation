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

        public static BaseResponse<T> Fail(IEnumerable<string> errors) => new()
        {
            Success = false,
            Errors = errors.ToList()
        };

        public static BaseResponse<T> Fail(string error) => new()
        {
            Success = false,
            Errors = [error]
        };
    }

}
