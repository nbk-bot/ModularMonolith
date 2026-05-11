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
        await db.SaveChangesAsync(ct);

        await bus.Publish(new ProductCreatedIntegrationEvent(product.Id, product.Name, product.Price), ct);
        return product.ToDto();
    }
}
