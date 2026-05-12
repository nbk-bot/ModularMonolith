using Catalog.Application.Contracts;
using Catalog.Application.Features.CreateProduct;
using Catalog.Application.IntegrationEvents;
using Catalog.Application.Mappers;
using Catalog.Domain;
using Catalog.Infrastructure.Persistence;
using MassTransit;
using MediatR;

namespace Catalog.Infrastructure.Features.CreateProduct;

internal sealed class CreateProductCommandHandler(CatalogDbContext db, IPublishEndpoint bus)
    : IRequestHandler<CreateProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken ct)
    {
        var product = Product.Create(request.Name, request.Price, request.Stock, request.Description);
        db.Products.Add(product);

        // With MassTransit's EF Core outbox enabled (see AddMessaging registration),
        // calling IPublishEndpoint.Publish inside the SaveChangesAsync ambient scope
        // captures the message into the outbox table and only actually dispatches it
        // to RabbitMQ after the SaveChangesAsync transaction succeeds.
        await bus.Publish(new ProductCreatedIntegrationEvent(product.Id, product.Name, product.Price), ct);

        await db.SaveChangesAsync(ct);

        return product.ToDto();
    }
}
