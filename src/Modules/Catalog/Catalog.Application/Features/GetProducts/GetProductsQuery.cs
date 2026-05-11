using Catalog.Application.Contracts;
using MediatR;

namespace Catalog.Application.Features.GetProducts;

public sealed record GetProductsQuery(int Page = 1, int PageSize = 20) : IRequest<IReadOnlyList<ProductDto>>;
