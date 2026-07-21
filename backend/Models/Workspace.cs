using System.ComponentModel.DataAnnotations;

namespace InventoryApi.Models;

public static class StockPilotClaimTypes
{
    public const string WorkspaceId = "workspace_id";
}

public sealed class Workspace
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(120)]
    public required string Name { get; set; }

    [MaxLength(80)]
    public required string Slug { get; set; }

    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<WorkspaceMember> Members { get; set; } = [];
    public ICollection<Category> Categories { get; set; } = [];
    public ICollection<InventoryItem> InventoryItems { get; set; } = [];
}

public sealed class WorkspaceMember
{
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    [MaxLength(20)]
    public string Role { get; set; } = AppRoles.Viewer;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
