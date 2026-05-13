using BuildingBlocks.Infrastructure.Persistence;
using Catalog.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : BaseDbContext(options)
{
    public const string DefaultSchema = "catalog";
    protected override string Schema => DefaultSchema;

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

        // MassTransit EF Core outbox tables. Migration is generated separately
        // (see docs/integrations.md → "MassTransit EF Outbox").
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
