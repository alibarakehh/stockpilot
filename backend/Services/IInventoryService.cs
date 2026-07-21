using InventoryApi.Contracts;

namespace InventoryApi.Services;

public interface IInventoryService
{
    Task<PagedResult<InventoryItemResponse>> SearchAsync(
        Guid workspaceId,
        InventoryQuery query,
        CancellationToken cancellationToken);

    Task<PagedResult<InventoryItemResponse>> SearchArchivedAsync(
        Guid workspaceId,
        InventoryQuery query,
        CancellationToken cancellationToken);

    Task<InventoryItemResponse?> GetAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);
    Task<InventorySummaryResponse> GetSummaryAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetCategoriesAsync(Guid workspaceId, CancellationToken cancellationToken);

    Task<PagedResult<InventoryMovementResponse>> GetMovementsAsync(
        Guid workspaceId,
        MovementQuery query,
        CancellationToken cancellationToken);

    Task<InventoryItemResponse> CreateAsync(
        SaveInventoryItemRequest request,
        StockActor actor,
        CancellationToken cancellationToken);

    Task<InventoryItemResponse?> UpdateAsync(
        Guid id,
        SaveInventoryItemRequest request,
        StockActor actor,
        CancellationToken cancellationToken);

    Task<InventoryItemResponse?> AdjustStockAsync(
        Guid id,
        StockAdjustmentRequest request,
        StockActor actor,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, StockActor actor, CancellationToken cancellationToken);
    Task<InventoryItemResponse?> RestoreAsync(
        Guid id,
        StockActor actor,
        CancellationToken cancellationToken);
}

public sealed class DuplicateSkuException(string sku) : Exception($"SKU '{sku}' is already in use.");
public sealed class InvalidStockAdjustmentException() : Exception("The adjustment would make quantity negative.");
public sealed class InvalidMovementDirectionException(string message) : Exception(message);
public sealed class DiscontinuedItemException() : Exception("Reactivate this item before changing its stock.");
public sealed class QuantityRequiresAdjustmentException() : Exception("Use a stock adjustment to change quantity so the movement is recorded.");
public sealed class InventoryConcurrencyException() : Exception("This item changed since you opened it. Refresh and try again.");
public sealed class IdempotencyConflictException() : Exception("This request identifier was already used for a different stock movement.");
