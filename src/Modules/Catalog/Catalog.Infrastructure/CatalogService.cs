using Catalog.Application;
using Catalog.Application.Contracts;
using Catalog.Application.Features.CreateProduct;
using Catalog.Application.IntegrationEvents;
using Catalog.Application.Mappers;
using Catalog.Domain;
using Catalog.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Infrastructure;

/// <summary>
/// Single Fusion compute service that subsumes the former MediatR
/// <c>CreateProductCommandHandler</c> + <c>GetProductsQueryHandler</c>. All
/// methods are <c>virtual</c> so Fusion can intercept them. <c>CreateProduct</c>
/// keeps the MassTransit EF Core outbox semantics — publishing before
/// <c>SaveChangesAsync</c> so the message is committed atomically.
/// Registered as singleton (Fusion's default) so per-call scoped dependencies
/// (DbContext, IPublishEndpoint) must be resolved through IServiceScopeFactory.
/// </summary>
public class CatalogService(IServiceScopeFactory scopeFactory) : ICatalogService
{
    public virtual async Task<ProductDto> CreateProduct(CreateProductCommand command, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var product = Product.Create(command.Name, command.Price, command.Stock, command.Description);
        db.Products.Add(product);

        // With MassTransit's EF Core outbox enabled (see AddMessaging registration),
        // calling IPublishEndpoint.Publish inside the SaveChangesAsync ambient scope
        // captures the message into the outbox table and only actually dispatches it
        // to RabbitMQ after the SaveChangesAsync transaction succeeds.
        await bus.Publish(new ProductCreatedIntegrationEvent(product.Id, product.Name, product.Price), ct);

        await db.SaveChangesAsync(ct);

        return product.ToDto();
    }

    public virtual async Task<IReadOnlyList<ProductDto>> GetProducts(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var items = await db.Products
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return items.ToDtoList();
    }
}
