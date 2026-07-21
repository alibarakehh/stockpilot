using System.ComponentModel.DataAnnotations;

namespace InventoryApi.Models;

public sealed class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    [MaxLength(100)]
    public required string Name { get; set; }

    [MaxLength(100)]
    public required string NormalizedName { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<InventoryItem> InventoryItems { get; set; } = [];
}
