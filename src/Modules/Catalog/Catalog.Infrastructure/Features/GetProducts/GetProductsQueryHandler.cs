using Catalog.Application.Contracts;
using Catalog.Application.Features.GetProducts;
using Catalog.Application.Mappers;
using Catalog.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Features.GetProducts;

internal sealed class GetProductsQueryHandler(CatalogDbContext db)
    : IRequestHandler<GetProductsQuery, IReadOnlyList<ProductDto>>
{
    public async Task<IReadOnlyList<ProductDto>> Handle(GetProductsQuery request, CancellationToken ct)
    {
        var items = await db.Products
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);
        return items.ToDtoList();
    }
}
