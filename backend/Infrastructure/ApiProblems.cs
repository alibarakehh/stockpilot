using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InventoryApi.Infrastructure;

public static class ApiErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string AuthenticationRequired = "authentication_required";
    public const string PermissionDenied = "permission_denied";
    public const string ResourceNotFound = "resource_not_found";
    public const string AntiforgeryFailed = "antiforgery_failed";
    public const string InvalidCredentials = "invalid_credentials";
    public const string AccountLocked = "account_locked";
    public const string InvalidRole = "invalid_role";
    public const string DuplicateUser = "duplicate_user";
    public const string SelfRoleChange = "self_role_change";
    public const string MethodNotAllowed = "method_not_allowed";
    public const string RateLimitExceeded = "rate_limit_exceeded";
    public const string RequestTooLarge = "request_too_large";
    public const string DuplicateSku = "duplicate_sku";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string IdempotencyConflict = "idempotency_conflict";
    public const string ItemDiscontinued = "item_discontinued";
    public const string InvalidStockAdjustment = "invalid_stock_adjustment";
    public const string InvalidMovementDirection = "invalid_movement_direction";
    public const string QuantityAdjustmentRequired = "quantity_adjustment_required";
    public const string UnexpectedError = "unexpected_error";
    public const string AiProviderUnavailable = "ai_provider_unavailable";
    public const string AiInvalidOutput = "ai_invalid_output";
}

public static class ApiProblems
{
    public static ProblemDetails Create(
        HttpContext httpContext,
        int status,
        string title,
        string code,
        string? detail = null)
    {
        var details = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        Enrich(details, httpContext, code);
        return details;
    }

    public static void Enrich(ProblemDetails details, HttpContext httpContext, string? code = null)
    {
        details.Status ??= httpContext.Response.StatusCode;
        details.Instance ??= httpContext.Request.Path;
        details.Extensions.TryAdd("code", code ?? CodeForStatus(details.Status.Value));
        details.Extensions.TryAdd(
            "traceId",
            Activity.Current?.Id ?? httpContext.TraceIdentifier);
    }

    public static async Task WriteAsync(
        HttpContext httpContext,
        int status,
        string title,
        string code,
        string? detail = null)
    {
        httpContext.Response.StatusCode = status;
        var details = Create(httpContext, status, title, code, detail);
        var service = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await service.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = details
        });
    }

    public static string CodeForStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => ApiErrorCodes.ValidationFailed,
        StatusCodes.Status401Unauthorized => ApiErrorCodes.AuthenticationRequired,
        StatusCodes.Status403Forbidden => ApiErrorCodes.PermissionDenied,
        StatusCodes.Status404NotFound => ApiErrorCodes.ResourceNotFound,
        StatusCodes.Status405MethodNotAllowed => ApiErrorCodes.MethodNotAllowed,
        StatusCodes.Status409Conflict => ApiErrorCodes.ConcurrencyConflict,
        StatusCodes.Status413PayloadTooLarge => ApiErrorCodes.RequestTooLarge,
        StatusCodes.Status429TooManyRequests => ApiErrorCodes.RateLimitExceeded,
        _ => ApiErrorCodes.UnexpectedError
    };
}
