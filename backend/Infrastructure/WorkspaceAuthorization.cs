using System.Security.Claims;
using InventoryApi.Data;
using InventoryApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Infrastructure;

public sealed record WorkspacePermissionRequirement(params string[] AllowedRoles)
    : IAuthorizationRequirement;

public sealed class WorkspacePermissionHandler(AppDbContext db)
    : AuthorizationHandler<WorkspacePermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        WorkspacePermissionRequirement requirement)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !Guid.TryParse(
                context.User.FindFirstValue(StockPilotClaimTypes.WorkspaceId),
                out var workspaceId))
        {
            return;
        }

        var role = await db.WorkspaceMembers.AsNoTracking()
            .Where(member => member.WorkspaceId == workspaceId && member.UserId == userId)
            .Select(member => member.Role)
            .SingleOrDefaultAsync();
        if (role is null) return;

        if (requirement.AllowedRoles.Length == 0 ||
            requirement.AllowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
    }
}
