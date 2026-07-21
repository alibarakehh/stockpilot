using System.ComponentModel.DataAnnotations;

namespace InventoryApi.Contracts;

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record UserResponse(Guid Id, string Name, string Email, string Role);

public sealed record AuthResponse(DateTime ExpiresAtUtc, UserResponse User);

public sealed record AntiforgeryResponse(string RequestToken);

public sealed class CreateUserRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public required string Name { get; init; }

    [Required, EmailAddress, StringLength(200)]
    public required string Email { get; init; }

    [Required, StringLength(100, MinimumLength = 10)]
    public required string Password { get; init; }

    [Required]
    public required string Role { get; init; }
}

public sealed record ChangeRoleRequest([Required] string Role);
