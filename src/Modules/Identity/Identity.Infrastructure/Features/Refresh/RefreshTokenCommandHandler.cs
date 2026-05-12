using BuildingBlocks.Infrastructure.Authentication;
using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Application.Features.Refresh;
using Identity.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Features.Refresh;

internal sealed class RefreshTokenCommandHandler(
    ITokenService tokens,
    IdentityDbContext db,
    IOptions<JwtOptions> jwtOpts) : IRequestHandler<RefreshTokenCommand, AuthTokens>
{
    public async Task<AuthTokens> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.RefreshToken == request.RefreshToken, ct)
            ?? throw new UnauthorizedAccessException("Invalid refresh token");

        if (user.RefreshTokenExpiresAt is null || user.RefreshTokenExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expired");

        var (access, expiresAt) = tokens.CreateAccessToken(user.Id, user.Email!, []);
        var refresh = tokens.CreateRefreshToken();

        user.RefreshToken = refresh;
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(jwtOpts.Value.RefreshTokenDays);
        await db.SaveChangesAsync(ct);

        return new AuthTokens(access, refresh, expiresAt);
    }
}
