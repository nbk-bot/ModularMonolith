using BuildingBlocks.Application.Abstractions;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure;

/// <summary>
/// EF Core implementation of <see cref="IRefreshTokenStore"/>. Issues a single
/// <c>ExecuteUpdateAsync</c> to null-out expired refresh tokens on
/// <c>ApplicationUser</c>.
/// </summary>
public sealed class RefreshTokenStore(IdentityDbContext db) : IRefreshTokenStore
{
    public Task<int> RemoveExpiredAsync(CancellationToken ct = default)
        => db.Users.Where(u => u.RefreshTokenExpiresAt != null && u.RefreshTokenExpiresAt < DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.RefreshToken, (string?)null)
                .SetProperty(u => u.RefreshTokenExpiresAt, (DateTime?)null), ct);
}
