using System.Security.Claims;
using InventoryApi.Contracts;
using InventoryApi.Models;
using InventoryApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryApi.Controllers;

[ApiController]
[Authorize]
[Route("api/inventory")]
public sealed class InventoryController(IInventoryService inventoryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<InventoryItemResponse>>> Search(
        [FromQuery] InventoryQuery query, CancellationToken cancellationToken) =>
        Ok(await inventoryService.SearchAsync(CurrentWorkspaceId(), query, cancellationToken));

    [HttpGet("archived")]
    [Authorize(Policy = StockPilotPolicies.ArchiveInventory)]
    public async Task<ActionResult<PagedResult<InventoryItemResponse>>> SearchArchived(
        [FromQuery] InventoryQuery query,
        CancellationToken cancellationToken) =>
        Ok(await inventoryService.SearchArchivedAsync(CurrentWorkspaceId(), query, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InventoryItemResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await inventoryService.GetAsync(CurrentWorkspaceId(), id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<InventorySummaryResponse>> Summary(CancellationToken cancellationToken) =>
        Ok(await inventoryService.GetSummaryAsync(CurrentWorkspaceId(), cancellationToken));

    [HttpGet("movements")]
    public async Task<ActionResult<PagedResult<InventoryMovementResponse>>> Movements(
        [FromQuery] MovementQuery query,
        CancellationToken cancellationToken) =>
        Ok(await inventoryService.GetMovementsAsync(CurrentWorkspaceId(), query, cancellationToken));

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<string>>> Categories(CancellationToken cancellationToken) =>
        Ok(await inventoryService.GetCategoriesAsync(CurrentWorkspaceId(), cancellationToken));

    [HttpPost]
    [Authorize(Policy = StockPilotPolicies.ManageInventory)]
    public async Task<ActionResult<InventoryItemResponse>> Create(
        SaveInventoryItemRequest request, CancellationToken cancellationToken)
    {
        var item = await inventoryService.CreateAsync(request, CurrentActor(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = StockPilotPolicies.ManageInventory)]
    public async Task<ActionResult<InventoryItemResponse>> Update(
        Guid id, SaveInventoryItemRequest request, CancellationToken cancellationToken)
    {
        var item = await inventoryService.UpdateAsync(id, request, CurrentActor(), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPatch("{id:guid}/stock")]
    [Authorize(Policy = StockPilotPolicies.ManageInventory)]
    public async Task<ActionResult<InventoryItemResponse>> AdjustStock(
        Guid id, StockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var item = await inventoryService.AdjustStockAsync(id, request, CurrentActor(), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = StockPilotPolicies.ArchiveInventory)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await inventoryService.DeleteAsync(id, CurrentActor(), cancellationToken) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/restore")]
    [Authorize(Policy = StockPilotPolicies.ArchiveInventory)]
    public async Task<ActionResult<InventoryItemResponse>> Restore(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await inventoryService.RestoreAsync(id, CurrentActor(), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    private StockActor CurrentActor()
    {
        var id = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : (Guid?)null;
        return new StockActor(CurrentWorkspaceId(), id, User.Identity?.Name ?? "Unknown user");
    }

    private Guid CurrentWorkspaceId() =>
        Guid.TryParse(User.FindFirstValue(StockPilotClaimTypes.WorkspaceId), out var workspaceId)
            ? workspaceId
            : throw new InvalidOperationException("The authenticated session has no workspace.");
}
