using System.ComponentModel.DataAnnotations;

namespace InventoryApi.Contracts;

public sealed class AiInventoryDraftRequest
{
    [Required, StringLength(2000, MinimumLength = 10)]
    public required string Description { get; init; }
}

public sealed record AiInventoryDraftResponse(
    string Name,
    string Sku,
    string Description,
    string Category,
    int Quantity,
    int ReorderLevel,
    decimal PurchasePrice,
    decimal SellingPrice,
    string Supplier,
    string Location,
    IReadOnlyList<string> GeneratedFields,
    IReadOnlyList<string> Warnings);

public sealed record AiSmartIntakeAvailabilityResponse(
    bool Available,
    string Provider,
    string? Reason);
