using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Donation.Api.Models.Common;
using Microsoft.EntityFrameworkCore;

namespace Donation.Api.Middlewares;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (OperationCanceledException oce) when (ctx.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation(oce, "Request aborted by client. TraceId: {TraceId}", ctx.TraceIdentifier);
            await WriteFail(ctx, 499, "Request was canceled by the client.");
        }
        catch (ValidationException vex) // DataAnnotations
        {
            _logger.LogWarning(vex, "Validation error. TraceId: {TraceId}", ctx.TraceIdentifier);
            var errors = new List<string>();

            if (vex.ValidationResult is not null && vex.ValidationResult.MemberNames?.Any() == true)
            {
                errors.AddRange(vex.ValidationResult.MemberNames.Select(m => $"{m}: {vex.ValidationResult!.ErrorMessage}"));
            }
            else
            {
                errors.Add(vex.Message);
            }

            await WriteFail(ctx, (int)HttpStatusCode.BadRequest, errors);
        }
        catch (ArgumentException aex)
        {
            _logger.LogWarning(aex, "Bad request. TraceId: {TraceId}", ctx.TraceIdentifier);
            await WriteFail(ctx, (int)HttpStatusCode.BadRequest, aex.Message);
        }
        catch (KeyNotFoundException knf)
        {
            _logger.LogWarning(knf, "Not found. TraceId: {TraceId}", ctx.TraceIdentifier);
            await WriteFail(ctx, (int)HttpStatusCode.NotFound, knf.Message);
        }
        catch (UnauthorizedAccessException uae)
        {
            _logger.LogWarning(uae, "Unauthorized. TraceId: {TraceId}", ctx.TraceIdentifier);
            await WriteFail(ctx, (int)HttpStatusCode.Unauthorized, "Unauthorized.");
        }
        catch (DbUpdateConcurrencyException dce)
        {
            _logger.LogWarning(dce, "Concurrency conflict. TraceId: {TraceId}", ctx.TraceIdentifier);
            await WriteFail(ctx, (int)HttpStatusCode.Conflict, "A concurrency conflict occurred. Please retry.");
        }
        catch (NotImplementedException nie)
        {
            _logger.LogError(nie, "Not implemented. TraceId: {TraceId}", ctx.TraceIdentifier);
            await WriteFail(ctx, (int)HttpStatusCode.NotImplemented, "This endpoint/feature is not implemented.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception. TraceId: {TraceId}", ctx.TraceIdentifier);
            var msg = _env.IsDevelopment()
                ? $"Unhandled error: {ex.Message}"
                : "An unexpected error occurred.";
            await WriteFail(ctx, (int)HttpStatusCode.InternalServerError, msg);
        }
    }

    private async Task WriteFail(HttpContext ctx, int statusCode, string error)
        => await WriteFail(ctx, statusCode, new List<string> { error });

    private async Task WriteFail(HttpContext ctx, int statusCode, IEnumerable<string> errors)
    {
        if (!ctx.Response.HasStarted)
        {
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = "application/json";
            ctx.Response.Headers["x-trace-id"] = ctx.TraceIdentifier;
        }

        var body = BaseResponse<object>.Fail(_env.IsDevelopment()
            ? errors.Select(e => $"{e} (traceId: {ctx.TraceIdentifier})")
            : errors);

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(body, Json));
    }
}
