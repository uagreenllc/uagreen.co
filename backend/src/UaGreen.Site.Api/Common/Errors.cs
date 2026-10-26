using System.Text.Json;

namespace UaGreen.Site.Api.Common;

public sealed record ApiError(string Code, string Message);

public static class ErrorCodes
{
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string InvalidToken = "INVALID_TOKEN";
    public const string RateLimited = "RATE_LIMITED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Conflict = "CONFLICT";
    public const string InvalidSignature = "INVALID_SIGNATURE";
    public const string InternalError = "INTERNAL_ERROR";
}

public sealed class ProblemException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }

    public ProblemException(int statusCode, string code, string message) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public static ProblemException BadRequest(string message, string code = ErrorCodes.ValidationFailed) => new(400, code, message);
    public static ProblemException Unauthorized(string message, string code = ErrorCodes.InvalidToken) => new(401, code, message);
    public static ProblemException Forbidden(string message) => new(403, ErrorCodes.Forbidden, message);
    public static ProblemException NotFound(string message) => new(404, ErrorCodes.NotFound, message);
    public static ProblemException Conflict(string message) => new(409, ErrorCodes.Conflict, message);
}

/// Renders ProblemException as {code, message}; everything else as a 500 (logged).
public sealed class ProblemMiddleware(RequestDelegate next, ILogger<ProblemMiddleware> log)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (ProblemException ex)
        {
            await Write(ctx, ex.StatusCode, new ApiError(ex.Code, ex.Message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Unhandled exception");
            await Write(ctx, 500, new ApiError(ErrorCodes.InternalError, "Internal server error"));
        }
    }

    private static async Task Write(HttpContext ctx, int status, ApiError error)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(error, Json));
    }
}
