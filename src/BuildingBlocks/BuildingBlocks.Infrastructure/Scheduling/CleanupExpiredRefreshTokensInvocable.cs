using BuildingBlocks.Application.Abstractions;
using Coravel.Invocable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Scheduling;

/// <summary>
/// Sample Coravel job: nightly cleanup of expired refresh tokens stored on
/// <c>ApplicationUser</c>. Depends on <see cref="IRefreshTokenStore"/> so the
/// BuildingBlock has no project-level reference to the Identity module — the
/// Identity infrastructure registers the implementation.
/// </summary>
public sealed class CleanupExpiredRefreshTokensInvocable(
    IServiceProvider services,
    ILogger<CleanupExpiredRefreshTokensInvocable> logger) : IInvocable
{
    public async Task Invoke()
    {
        using var scope = services.CreateScope();
        var store = scope.ServiceProvider.GetService<IRefreshTokenStore>();
        if (store is null)
        {
            logger.LogDebug("IRefreshTokenStore not registered; skipping refresh-token cleanup.");
            return;
        }

        var changed = await store.RemoveExpiredAsync();
        if (changed > 0)
            logger.LogInformation("Cleaned up {Count} expired refresh tokens.", changed);
    }
}
