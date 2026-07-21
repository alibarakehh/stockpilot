using System.ComponentModel.DataAnnotations;
using InventoryApi.Models;

namespace InventoryApi.Contracts;

public sealed class InventoryQuery : IValidatableObject
{
    [StringLength(200)]
    public string? Search { get; init; }

    [StringLength(100)]
    public string? Category { get; init; }

    [StringLength(160)]
    public string? Supplier { get; init; }

    [StringLength(120)]
    public string? Location { get; init; }

    [Range(0, int.MaxValue)]
    public int? MinQuantity { get; init; }

    [Range(0, int.MaxValue)]
    public int? MaxQuantity { get; init; }

    public StockStatus? Status { get; init; }

    [RegularExpression(
        "(?i)^(updated|name|sku|category|quantity|purchasePrice|sellingPrice|value|location|supplier)$",
        ErrorMessage = "SortBy is not supported.")]
    public string SortBy { get; init; } = "updated";
    public bool Descending { get; init; } = true;

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinQuantity is not null && MaxQuantity is not null && MinQuantity > MaxQuantity)
        {
            yield return new ValidationResult(
                "MinQuantity cannot be greater than MaxQuantity.",
                [nameof(MinQuantity), nameof(MaxQuantity)]);
        }
    }
}

public sealed record InventoryItemResponse(
    Guid Id,
    string Name,
    string Sku,
    string Category,
    string Description,
    string Location,
    string Supplier,
    int Quantity,
    int ReorderLevel,
    decimal PurchasePrice,
    decimal SellingPrice,
    decimal InventoryValue,
    string CurrencyCode,
    InventoryLifecycleStatus LifecycleStatus,
    ProcurementStatus ProcurementStatus,
    StockStatus Status,
    bool IsArchived,
    DateTime? DeletedAtUtc,
    long Version,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed class SaveInventoryItemRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public required string Name { get; init; }

    [Required, StringLength(80, MinimumLength = 2)]
    public required string Sku { get; init; }

    [Required, StringLength(100, MinimumLength = 2)]
    public required string Category { get; init; }

    [StringLength(1000)]
    public string Description { get; init; } = string.Empty;

    [StringLength(120)]
    public string Location { get; init; } = string.Empty;

    [StringLength(160)]
    public string Supplier { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Quantity { get; init; }

    [Range(0, int.MaxValue)]
    public int ReorderLevel { get; init; } = 5;

    [Range(typeof(decimal), "0", "999999999")]
    public decimal PurchasePrice { get; init; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal SellingPrice { get; init; }

    public InventoryLifecycleStatus LifecycleStatus { get; init; } = InventoryLifecycleStatus.Active;
    public ProcurementStatus ProcurementStatus { get; init; } = ProcurementStatus.None;

    public long? Version { get; init; }
}

public sealed class StockAdjustmentRequest
{
    public Guid RequestId { get; init; }

    [Range(-1000000, 1000000)]
    public int Change { get; init; }

    public MovementType Type { get; init; }

    [Required, StringLength(300, MinimumLength = 2)]
    public required string Reason { get; init; }

    [Range(1, long.MaxValue)]
    public long? Version { get; init; }
}

public sealed record InventorySummaryResponse(
    int TotalItems,
    int TotalUnits,
    decimal TotalValue,
    string CurrencyCode,
    int InStockCount,
    int LowStockCount,
    int OutOfStockCount,
    int OrderedCount,
    int DiscontinuedCount);

public sealed record InventoryMovementResponse(
    Guid Id,
    Guid RequestId,
    Guid ItemId,
    string ItemName,
    string Sku,
    MovementType Type,
    int Change,
    int PreviousQuantity,
    int NewQuantity,
    string Reason,
    string PerformedByName,
    DateTime CreatedAtUtc);

public sealed class MovementQuery
{
    public Guid? ItemId { get; init; }

    [Range(1, 1_000_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record StockActor(Guid WorkspaceId, Guid? UserId, string Name);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(Total / (double)PageSize);
}
