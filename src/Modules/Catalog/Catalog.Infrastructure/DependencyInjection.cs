using ActualLab.Fusion;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.Persistence;
using Catalog.Application;
using Catalog.Infrastructure.Persistence;
using FluentValidation;
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

        // Fusion compute service: replaces MediatR per-feature handler scan.
        // Singleton (Fusion default) — service resolves scoped DbContext/IPublishEndpoint
        // through IServiceScopeFactory per-call so DI lifetimes remain correct.
        services.AddFusion().AddService<ICatalogService, CatalogService>();

        // FluentValidation discovers Catalog.Application validators here
        // (CreateProductCommandValidator etc.).
        services.AddValidatorsFromAssembly(typeof(ICatalogService).Assembly);
        services.AddCommandValidation(typeof(ICatalogService).Assembly);

        return services;
    }
}
