using System.Security.Claims;
using InventoryApi.Contracts;
using InventoryApi.Infrastructure;
using InventoryApi.Models;
using InventoryApi.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InventoryApi.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("antiforgery")]
    [AllowAnonymous]
    public ActionResult<AntiforgeryResponse> Antiforgery()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new AntiforgeryResponse(
            tokens.RequestToken ?? throw new InvalidOperationException("Antiforgery token was not created.")));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.AuthenticateAsync(request, cancellationToken);
        if (result.Status != AuthenticationStatus.Success || result.User is null)
        {
            var locked = result.Status == AuthenticationStatus.LockedOut;
            return Unauthorized(ApiProblems.Create(
                HttpContext,
                StatusCodes.Status401Unauthorized,
                locked ? "Account temporarily locked" : "Sign-in failed",
                locked ? ApiErrorCodes.AccountLocked : ApiErrorCodes.InvalidCredentials,
                locked
                    ? "Sign-in is temporarily locked. Please try again later."
                    : "Email or password is incorrect."));
        }

        var identity = new ClaimsIdentity(result.Claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                AllowRefresh = false,
                ExpiresUtc = result.ExpiresAtUtc,
                IsPersistent = false
            });

        return Ok(new AuthResponse(result.ExpiresAtUtc, result.User));
    }

    [HttpGet("me")]
    [Authorize(Policy = StockPilotPolicies.AuthenticatedSession)]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !Guid.TryParse(User.FindFirstValue(StockPilotClaimTypes.WorkspaceId), out var workspaceId))
        {
            return Unauthorized();
        }

        var user = await authService.GetCurrentUserAsync(userId, workspaceId, cancellationToken);
        return user is null ? Unauthorized() : Ok(user);
    }

    [HttpPost("logout")]
    [Authorize(Policy = StockPilotPolicies.AuthenticatedSession)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
