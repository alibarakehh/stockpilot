using InventoryApi.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Infrastructure;

public sealed partial class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, code) = exception switch
        {
            DuplicateSkuException => (StatusCodes.Status409Conflict, "Duplicate SKU", ApiErrorCodes.DuplicateSku),
            InventoryConcurrencyException => (StatusCodes.Status409Conflict, "Inventory changed", ApiErrorCodes.ConcurrencyConflict),
            IdempotencyConflictException => (StatusCodes.Status409Conflict, "Request identifier conflict", ApiErrorCodes.IdempotencyConflict),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Inventory changed", ApiErrorCodes.ConcurrencyConflict),
            DiscontinuedItemException => (StatusCodes.Status409Conflict, "Item is discontinued", ApiErrorCodes.ItemDiscontinued),
            InvalidStockAdjustmentException => (StatusCodes.Status400BadRequest, "Invalid stock adjustment", ApiErrorCodes.InvalidStockAdjustment),
            InvalidMovementDirectionException => (StatusCodes.Status400BadRequest, "Invalid movement type", ApiErrorCodes.InvalidMovementDirection),
            QuantityRequiresAdjustmentException => (StatusCodes.Status400BadRequest, "Stock adjustment required", ApiErrorCodes.QuantityAdjustmentRequired),
            AiProviderUnavailableException => (StatusCodes.Status503ServiceUnavailable, "AI Smart Intake unavailable", ApiErrorCodes.AiProviderUnavailable),
            AiProviderException => (StatusCodes.Status503ServiceUnavailable, "AI extraction unavailable", ApiErrorCodes.AiProviderUnavailable),
            InvalidAiDraftException => (StatusCodes.Status422UnprocessableEntity, "AI draft was invalid", ApiErrorCodes.AiInvalidOutput),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error", ApiErrorCodes.UnexpectedError)
        };

        if (status == StatusCodes.Status500InternalServerError)
            LogUnhandledRequestFailure(logger, exception, httpContext.Request.Path.Value ?? "/");

        httpContext.Response.StatusCode = status;
        var details = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError
                ? "The request could not be completed. Try again or contact an administrator."
                : exception is AiProviderException
                    ? "AI extraction is temporarily unavailable. You can still enter the item manually."
                : exception is DbUpdateConcurrencyException
                    ? "This item changed since you opened it. Refresh and try again."
                : exception.Message,
            Instance = httpContext.Request.Path
        };
        ApiProblems.Enrich(details, httpContext, code);
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = details,
            Exception = exception
        });
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Unhandled request failure for {Path}")]
    private static partial void LogUnhandledRequestFailure(
        ILogger logger,
        Exception exception,
        string path);
}
