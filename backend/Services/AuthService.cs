using System.Security.Claims;
using InventoryApi.Contracts;
using InventoryApi.Data;
using InventoryApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InventoryApi.Services;

public sealed class AuthService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration) : IAuthService
{
    public async Task<AuthenticationResult> AuthenticateAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var normalizedEmail = userManager.NormalizeEmail(email);
        var user = await db.Users.SingleOrDefaultAsync(
            candidate => candidate.NormalizedEmail == normalizedEmail || candidate.Email == email,
            cancellationToken);
        if (user is null)
        {
            return Failed(AuthenticationStatus.InvalidCredentials);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Failed(AuthenticationStatus.LockedOut);
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            return Failed(await userManager.IsLockedOutAsync(user)
                ? AuthenticationStatus.LockedOut
                : AuthenticationStatus.InvalidCredentials);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        var membership = await db.WorkspaceMembers.AsNoTracking()
            .Include(candidate => candidate.Workspace)
            .Where(candidate => candidate.UserId == user.Id)
            .OrderBy(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (membership is null)
        {
            return Failed(AuthenticationStatus.InvalidCredentials);
        }

        var expiresAt = DateTime.UtcNow.AddHours(configuration.GetValue("Authentication:ExpiryHours", 8));
        var response = new UserResponse(user.Id, user.Name, user.Email ?? email, membership.Role);
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email ?? email),
            new(ClaimTypes.Role, membership.Role),
            new(StockPilotClaimTypes.WorkspaceId, membership.WorkspaceId.ToString())
        ];
        return new AuthenticationResult(AuthenticationStatus.Success, expiresAt, response, claims);
    }

    public async Task<UserResponse?> GetCurrentUserAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken) =>
        await db.WorkspaceMembers.AsNoTracking()
            .Where(member => member.WorkspaceId == workspaceId && member.UserId == userId)
            .Select(member => new UserResponse(
                member.User.Id,
                member.User.Name,
                member.User.Email ?? string.Empty,
                member.Role))
            .SingleOrDefaultAsync(cancellationToken);

    private static AuthenticationResult Failed(AuthenticationStatus status) =>
        new(status, DateTime.MinValue, null, []);
}
