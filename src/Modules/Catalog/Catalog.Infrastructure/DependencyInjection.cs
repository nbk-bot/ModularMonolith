using BuildingBlocks.Infrastructure.Persistence;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Postgres connection string missing");

        services.AddDbContext<CatalogDbContext>((sp, o) => o
            .UseNpgsql(conn, b => b.MigrationsHistoryTable("__ef_migrations_history", CatalogDbContext.DefaultSchema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<DomainEventDispatcherInterceptor>()));

        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        return services;
    }
}
