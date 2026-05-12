using Coravel.Invocable;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Scheduling;

/// <summary>
/// Sample Coravel job: nightly cleanup of expired refresh tokens stored on
/// <c>ApplicationUser</c>. Uses <see cref="IServiceProvider"/> directly because
/// the Identity module's <c>IdentityDbContext</c> lives in a different
/// assembly that this BuildingBlock cannot reference. The DbContext type is
/// resolved by full-name lookup at runtime to avoid a project-level cycle.
/// TODO: when an <c>IRefreshTokenStore</c> abstraction is added to
/// <c>BuildingBlocks.Application</c>, switch this to depend on it instead.
/// </summary>
public sealed class CleanupExpiredRefreshTokensInvocable(
    IServiceProvider services,
    ILogger<CleanupExpiredRefreshTokensInvocable> logger) : IInvocable
{
    public async Task Invoke()
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        // Resolve IdentityDbContext by name to avoid taking a reference on the Identity module.
        var dbContextType = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(a => a.GetType("Identity.Infrastructure.Persistence.IdentityDbContext"))
            .FirstOrDefault(t => t is not null);

        if (dbContextType is null)
        {
            logger.LogDebug("IdentityDbContext type not loaded; skipping refresh-token cleanup.");
            return;
        }

        if (sp.GetService(dbContextType) is not Microsoft.EntityFrameworkCore.DbContext db)
        {
            logger.LogDebug("IdentityDbContext not registered; skipping refresh-token cleanup.");
            return;
        }

        // Find the Users DbSet and clear expired refresh tokens.
        var userType = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(a => a.GetType("Identity.Domain.ApplicationUser"))
            .FirstOrDefault(t => t is not null);

        if (userType is null)
        {
            logger.LogDebug("ApplicationUser type not loaded; skipping refresh-token cleanup.");
            return;
        }

        var now = DateTime.UtcNow;
        var setMethod = typeof(Microsoft.EntityFrameworkCore.DbContext)
            .GetMethods()
            .First(m => m.Name == nameof(Microsoft.EntityFrameworkCore.DbContext.Set) && m.IsGenericMethod && m.GetParameters().Length == 0)
            .MakeGenericMethod(userType);
        var set = (System.Collections.IEnumerable?)setMethod.Invoke(db, null);
        if (set is null) return;

        var expiresProp = userType.GetProperty("RefreshTokenExpiresAt");
        var tokenProp = userType.GetProperty("RefreshToken");
        if (expiresProp is null || tokenProp is null) return;

        var changed = 0;
        foreach (var user in set)
        {
            var exp = (DateTime?)expiresProp.GetValue(user);
            if (exp is not null && exp <= now)
            {
                expiresProp.SetValue(user, null);
                tokenProp.SetValue(user, null);
                changed++;
            }
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Cleaned up {Count} expired refresh tokens.", changed);
        }
    }
}
