using System.Security.Claims;
using InventoryApi.Contracts;
using InventoryApi.Models;
using InventoryApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using InventoryApi.Infrastructure;

namespace InventoryApi.Controllers;

[ApiController]
[Authorize]
[Route("api/ai")]
public sealed class AiController(
    IAiInventoryService aiService,
    IAiInventoryDraftService draftService) : ControllerBase
{
    [HttpGet("insights")]
    public async Task<ActionResult<InventoryIntelligenceResponse>> Insights(CancellationToken cancellationToken)
    {
        var workspaceId = Guid.TryParse(
            User.FindFirstValue(StockPilotClaimTypes.WorkspaceId),
            out var parsedWorkspaceId)
            ? parsedWorkspaceId
            : throw new InvalidOperationException("The authenticated session has no workspace.");
        return Ok(await aiService.GenerateInsightsAsync(workspaceId, cancellationToken));
    }

    [HttpGet("inventory-draft/availability")]
    [Authorize(Policy = StockPilotPolicies.ManageInventory)]
    public ActionResult<AiSmartIntakeAvailabilityResponse> DraftAvailability() =>
        Ok(draftService.GetAvailability());

    [HttpPost("inventory-draft")]
    [Authorize(Policy = StockPilotPolicies.ManageInventory)]
    [EnableRateLimiting("ai")]
    [RequestSizeLimit(8192)]
    public async Task<ActionResult<AiInventoryDraftResponse>> InventoryDraft(
        AiInventoryDraftRequest request,
        CancellationToken cancellationToken) =>
        Ok(await draftService.GenerateAsync(request.Description, cancellationToken));
}
