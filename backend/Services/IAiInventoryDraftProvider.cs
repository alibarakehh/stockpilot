namespace InventoryApi.Services;

public interface IAiInventoryDraftProvider
{
    bool IsAvailable { get; }
    string ProviderName { get; }
    string? UnavailableReason { get; }

    Task<AiInventoryDraftCandidate> GenerateAsync(
        string description,
        CancellationToken cancellationToken);
}

public sealed record AiInventoryDraftCandidate(
    string Name,
    string Sku,
    string Description,
    string Category,
    int Quantity,
    int ReorderLevel,
    decimal PurchasePrice,
    decimal SellingPrice,
    string Supplier,
    string Location);
