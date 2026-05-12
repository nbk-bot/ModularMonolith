using Identity.Application.Contracts;
using Identity.Application.Features.ConfirmEmail;
using Identity.Application.Features.ForgotPassword;
using Identity.Application.Features.Login;
using Identity.Application.Features.Logout;
using Identity.Application.Features.Refresh;
using Identity.Application.Features.Register;
using Identity.Application.Features.ResetPassword;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Presentation;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register([FromBody] RegisterRequest req, CancellationToken ct)
        => Ok(await sender.Send(new RegisterCommand(req.Email, req.Password, req.FullName), ct));

    [HttpPost("login")]
    public async Task<ActionResult<AuthTokens>> Login([FromBody] LoginRequest req, CancellationToken ct)
        => Ok(await sender.Send(new LoginCommand(req.Email, req.Password), ct));

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthTokens>> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
        => Ok(await sender.Send(new RefreshTokenCommand(req.RefreshToken), ct));

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        await sender.Send(new ForgotPasswordCommand(req.Email), ct);
        return NoContent();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        await sender.Send(new ResetPasswordCommand(req.Email, req.Token, req.NewPassword), ct);
        return NoContent();
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest req, CancellationToken ct)
    {
        await sender.Send(new ConfirmEmailCommand(req.UserId, req.Token), ct);
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

        await sender.Send(new LogoutCommand(userId), ct);
        return NoContent();
    }
}
