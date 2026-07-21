using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace InventoryApi.Models;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Viewer = "Viewer";
    public const string CanManageInventory = Admin + "," + Manager;

    public static readonly string[] All = [Admin, Manager, Viewer];
}

public static class StockPilotPolicies
{
    public const string AuthenticatedSession = "AuthenticatedSession";
    public const string ManageInventory = "ManageInventory";
    public const string ArchiveInventory = "ArchiveInventory";
    public const string ManageTeam = "ManageTeam";
}

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.NewGuid();
        SecurityStamp = Guid.NewGuid().ToString();
    }

    [MaxLength(120)]
    public required string Name { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<WorkspaceMember> WorkspaceMemberships { get; set; } = [];
}
