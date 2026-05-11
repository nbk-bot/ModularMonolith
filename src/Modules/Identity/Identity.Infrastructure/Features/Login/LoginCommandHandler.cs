using BuildingBlocks.Infrastructure.Authentication;
using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Application.Features.Login;
using Identity.Domain;
using Identity.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Features.Login;

internal sealed class LoginCommandHandler(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    ITokenService tokens,
    IdentityDbContext db,
    IOptions<JwtOptions> jwtOpts) : IRequestHandler<LoginCommand, AuthTokens>
{
    public async Task<AuthTokens> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email)
            ?? throw new UnauthorizedAccessException("Invalid credentials");

        var check = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!check.Succeeded) throw new UnauthorizedAccessException("Invalid credentials");

        var roles = await users.GetRolesAsync(user);
        var (access, expiresAt) = tokens.CreateAccessToken(user.Id, user.Email!,
            roles.Select(r => new System.Security.Claims.Claim("role", r)));
        var refresh = tokens.CreateRefreshToken();

        user.RefreshToken = refresh;
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(jwtOpts.Value.RefreshTokenDays);
        await db.SaveChangesAsync(ct);

        return new AuthTokens(access, refresh, expiresAt);
    }
}
