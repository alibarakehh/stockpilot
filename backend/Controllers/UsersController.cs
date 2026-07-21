using System.Security.Claims;
using InventoryApi.Contracts;
using InventoryApi.Data;
using InventoryApi.Infrastructure;
using InventoryApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Controllers;

[ApiController]
[Authorize(Policy = StockPilotPolicies.ManageTeam)]
[Route("api/users")]
public sealed class UsersController(
    AppDbContext db,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> List(CancellationToken cancellationToken)
    {
        var workspaceId = CurrentWorkspaceId();
        var members = await db.WorkspaceMembers.AsNoTracking()
            .Where(member => member.WorkspaceId == workspaceId)
            .OrderBy(member => member.User.Name)
            .Select(member => new UserResponse(
                member.User.Id,
                member.User.Name,
                member.User.Email ?? string.Empty,
                member.Role))
            .ToListAsync(cancellationToken);
        return Ok(members);
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!AppRoles.All.Contains(request.Role, StringComparer.OrdinalIgnoreCase))
            return BadRequest(ApiProblems.Create(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "Invalid role",
                ApiErrorCodes.InvalidRole,
                "Role must be Admin, Manager, or Viewer."));

        var email = request.Email.Trim().ToLowerInvariant();
        var normalizedEmail = userManager.NormalizeEmail(email);
        if (await db.Users.AnyAsync(
                user => user.NormalizedEmail == normalizedEmail || user.Email == email,
                cancellationToken))
        {
            return Conflict(ApiProblems.Create(
                HttpContext,
                StatusCodes.Status409Conflict,
                "Member already exists",
                ApiErrorCodes.DuplicateUser,
                "A member with this email cannot be added."));
        }

        var role = AppRoles.All.Single(candidate =>
            candidate.Equals(request.Role, StringComparison.OrdinalIgnoreCase));
        var user = new ApplicationUser
        {
            Name = request.Name.Trim(),
            Email = email,
            UserName = email
        };
        var creation = await userManager.CreateAsync(user, request.Password);
        if (!creation.Succeeded)
        {
            foreach (var error in creation.Errors)
                ModelState.AddModelError(nameof(request.Password), error.Description);
            var details = new ValidationProblemDetails(ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Request validation failed",
                Instance = HttpContext.Request.Path
            };
            ApiProblems.Enrich(details, HttpContext, ApiErrorCodes.ValidationFailed);
            return BadRequest(details);
        }

        db.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = CurrentWorkspaceId(),
            User = user,
            Role = role
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await userManager.DeleteAsync(user);
            throw;
        }

        var response = new UserResponse(user.Id, user.Name, user.Email ?? email, role);
        return CreatedAtAction(nameof(List), response);
    }

    [HttpPatch("{id:guid}/role")]
    public async Task<ActionResult<UserResponse>> ChangeRole(
        Guid id,
        ChangeRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!AppRoles.All.Contains(request.Role, StringComparer.OrdinalIgnoreCase))
            return BadRequest(ApiProblems.Create(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "Invalid role",
                ApiErrorCodes.InvalidRole,
                "Role must be Admin, Manager, or Viewer."));

        var workspaceId = CurrentWorkspaceId();
        var member = await db.WorkspaceMembers
            .Include(candidate => candidate.User)
            .SingleOrDefaultAsync(
                candidate => candidate.WorkspaceId == workspaceId && candidate.UserId == id,
                cancellationToken);
        if (member is null) return NotFound();

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == id.ToString() &&
            !request.Role.Equals(AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiProblems.Create(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "Administrator role required",
                ApiErrorCodes.SelfRoleChange,
                "You cannot remove your own administrator access."));
        }

        member.Role = AppRoles.All.Single(role =>
            role.Equals(request.Role, StringComparison.OrdinalIgnoreCase));
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new UserResponse(
            member.User.Id,
            member.User.Name,
            member.User.Email ?? string.Empty,
            member.Role));
    }

    private Guid CurrentWorkspaceId() =>
        Guid.TryParse(User.FindFirstValue(StockPilotClaimTypes.WorkspaceId), out var workspaceId)
            ? workspaceId
            : throw new InvalidOperationException("The authenticated session has no workspace.");
}
