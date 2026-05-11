using Identity.Application.Contracts;
using Identity.Application.Features.Login;
using Identity.Application.Features.Register;
using MediatR;
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
}
