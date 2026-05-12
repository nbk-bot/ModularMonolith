using Catalog.Application.Features.GetProducts;
using Grpc.Core;
using MediatR;

namespace Catalog.Presentation.Grpc;

/// <summary>
/// gRPC entry point for the Catalog module. Pure transport — work is delegated
/// to the MediatR pipeline (<see cref="GetProductsQuery"/>) so REST, GraphQL
/// and gRPC share the same handlers/validators/behaviours.
/// </summary>
public sealed class CatalogGrpcService(ISender sender) : CatalogGrpc.CatalogGrpcBase
{
    public override async Task<ListProductsResponse> ListProducts(ListProductsRequest request, ServerCallContext context)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;
        var items = await sender.Send(new GetProductsQuery(page, pageSize), context.CancellationToken);

        var response = new ListProductsResponse();
        foreach (var p in items)
        {
            response.Items.Add(new Product
            {
                Id = p.Id.ToString(),
                Name = p.Name,
                Description = p.Description ?? string.Empty,
                Price = (double)p.Price,
                Stock = p.Stock,
                CreatedAt = p.CreatedAt.ToString("O"),
            });
        }
        return response;
    }
}
