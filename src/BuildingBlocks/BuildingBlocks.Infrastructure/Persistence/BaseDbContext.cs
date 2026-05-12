using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Base for module <see cref="DbContext"/>s that don't already inherit from
/// a framework-supplied context (e.g. ASP.NET Identity's
/// <c>IdentityDbContext&lt;...&gt;</c>). Just sets the Postgres default
/// schema; domain-event dispatch is handled by
/// <see cref="DomainEventDispatcherInterceptor"/>, which is added to <i>all</i>
/// module DbContexts in their respective <c>AddXxxModule</c> registrations.
/// </summary>
public abstract class BaseDbContext(DbContextOptions options) : DbContext(options)
{
    protected abstract string Schema { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        base.OnModelCreating(modelBuilder);
    }
}
