using System.ComponentModel.DataAnnotations;

namespace InventoryApi.Models;

public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    [MaxLength(80)]
    public required string EntityType { get; set; }

    public Guid EntityId { get; set; }

    [MaxLength(80)]
    public required string Action { get; set; }

    public Guid? ActorUserId { get; set; }

    [MaxLength(120)]
    public required string ActorName { get; set; }

    public string DetailsJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
