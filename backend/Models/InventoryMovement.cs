using System.ComponentModel.DataAnnotations;

namespace InventoryApi.Models;

public enum MovementType
{
    OpeningBalance,
    Receipt,
    Issue,
    Damage,
    Return,
    Correction
}

public sealed class InventoryMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public Guid InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public Guid RequestId { get; set; }
    public MovementType Type { get; set; }
    public int Change { get; set; }
    public int PreviousQuantity { get; set; }
    public int NewQuantity { get; set; }

    [MaxLength(300)]
    public required string Reason { get; set; }

    public Guid? PerformedByUserId { get; set; }

    [MaxLength(120)]
    public required string PerformedByName { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
