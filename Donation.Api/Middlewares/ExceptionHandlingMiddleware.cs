using Donation.Api.Models.Common;
using Donation.Core;
using Donation.Core.Enums;
using System.Net;
using System.Text.Json;

namespace Donation.Api.Middlewares
{
    public sealed class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        public ExceptionHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext ctx)
        {
            try
            {
                await _next(ctx);
            }
            catch (AppException appEx)
            {
                await WriteBaseResponseAsync(ctx, appEx.ErrorCode, appEx.Message, MapStatusCode(appEx.ErrorCode));
            }
            catch (OperationCanceledException)
            {
                await WriteBaseResponseAsync(ctx, GeneralError.UnexpectedError, "Request was cancelled.", 499);
            }
            catch (Exception)
            {
                await WriteBaseResponseAsync(ctx, GeneralError.UnexpectedError, "Unexpected error occurred.", (int)HttpStatusCode.InternalServerError);
            }
        }

        private static async Task WriteBaseResponseAsync(HttpContext ctx, GeneralError code, string? message, int httpStatus)
        {
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = httpStatus;

            var payload = BaseResponse<object>.Fail(code);

            await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOpts));
        }

        private static int MapStatusCode(GeneralError code) => code switch
        {
            GeneralError.Unauthorized => (int)HttpStatusCode.Unauthorized,
            GeneralError.UserNotFound => (int)HttpStatusCode.NotFound,

            _ => (int)HttpStatusCode.BadRequest
        };
    }
}
