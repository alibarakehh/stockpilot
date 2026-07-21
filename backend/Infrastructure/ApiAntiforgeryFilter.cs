using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InventoryApi.Infrastructure;

public sealed class ApiAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method) ||
            HttpMethods.IsHead(context.HttpContext.Request.Method) ||
            HttpMethods.IsOptions(context.HttpContext.Request.Method) ||
            HttpMethods.IsTrace(context.HttpContext.Request.Method))
        {
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            var details = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Antiforgery validation failed",
                Detail = "Refresh the page and try the request again."
            };
            ApiProblems.Enrich(details, context.HttpContext, ApiErrorCodes.AntiforgeryFailed);
            context.Result = new BadRequestObjectResult(details);
        }
    }
}
