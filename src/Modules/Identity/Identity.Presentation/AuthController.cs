using Identity.Application;
using Identity.Application.Contracts;
using Identity.Application.Features.ConfirmEmail;
using Identity.Application.Features.ForgotPassword;
using Identity.Application.Features.Login;
using Identity.Application.Features.Logout;
using Identity.Application.Features.Refresh;
using Identity.Application.Features.Register;
using Identity.Application.Features.ResetPassword;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Presentation;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IIdentityService identity) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register([FromBody] RegisterRequest req, CancellationToken ct)
        => Ok(await identity.Register(new RegisterCommand(req.Email, req.Password, req.FullName), ct));

    [HttpPost("login")]
    public async Task<ActionResult<AuthTokens>> Login([FromBody] LoginRequest req, CancellationToken ct)
        => Ok(await identity.Login(new LoginCommand(req.Email, req.Password), ct));

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthTokens>> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
        => Ok(await identity.Refresh(new RefreshTokenCommand(req.RefreshToken), ct));

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        await identity.ForgotPassword(new ForgotPasswordCommand(req.Email), ct);
        return NoContent();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        await identity.ResetPassword(new ResetPasswordCommand(req.Email, req.Token, req.NewPassword), ct);
        return NoContent();
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest req, CancellationToken ct)
    {
        await identity.ConfirmEmail(new ConfirmEmailCommand(req.UserId, req.Token), ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var sub = User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Missing subject claim");
        if (!Guid.TryParse(sub, out var userId))
            throw new UnauthorizedAccessException("Invalid subject claim");

        await identity.Logout(new LogoutCommand(userId), ct);
        return NoContent();
    }
}
