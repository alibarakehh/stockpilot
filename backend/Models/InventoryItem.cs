using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InventoryApi.Models;

public enum StockStatus
{
    InStock,
    LowStock,
    OutOfStock,
    Ordered,
    Discontinued
}

public enum InventoryLifecycleStatus
{
    Active,
    Discontinued
}

public enum ProcurementStatus
{
    None,
    Ordered
}

public static class InventoryStatus
{
    public static StockStatus Calculate(
        InventoryLifecycleStatus lifecycleStatus,
        ProcurementStatus procurementStatus,
        int quantity,
        int reorderLevel)
    {
        if (lifecycleStatus == InventoryLifecycleStatus.Discontinued) return StockStatus.Discontinued;
        if (procurementStatus == ProcurementStatus.Ordered) return StockStatus.Ordered;
        if (quantity == 0) return StockStatus.OutOfStock;
        return quantity <= reorderLevel ? StockStatus.LowStock : StockStatus.InStock;
    }
}

public sealed class InventoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    [MaxLength(160)]
    public required string Name { get; set; }

    [MaxLength(80)]
    public required string Sku { get; set; }

    [MaxLength(80)]
    public required string NormalizedSku { get; set; }

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(160)]
    public string Supplier { get; set; } = string.Empty;

    public int Quantity { get; set; }
    public int ReorderLevel { get; set; } = 5;
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public InventoryLifecycleStatus LifecycleStatus { get; set; } = InventoryLifecycleStatus.Active;
    public ProcurementStatus ProcurementStatus { get; set; } = ProcurementStatus.None;

    [NotMapped]
    public StockStatus Status => InventoryStatus.Calculate(
        LifecycleStatus,
        ProcurementStatus,
        Quantity,
        ReorderLevel);

    public long Version { get; set; } = 1;
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<InventoryMovement> Movements { get; set; } = [];
}
