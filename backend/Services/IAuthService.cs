using InventoryApi.Contracts;
using System.Security.Claims;

namespace InventoryApi.Services;

public interface IAuthService
{
    Task<AuthenticationResult> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<UserResponse?> GetCurrentUserAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken);
}

public enum AuthenticationStatus
{
    Success,
    InvalidCredentials,
    LockedOut
}

public sealed record AuthenticationResult(
    AuthenticationStatus Status,
    DateTime ExpiresAtUtc,
    UserResponse? User,
    IReadOnlyList<Claim> Claims);
