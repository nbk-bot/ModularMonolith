using ActualLab.CommandR;
using ActualLab.CommandR.Configuration;
using ActualLab.Fusion;
using Catalog.Application.Contracts;
using Catalog.Application.Features.CreateProduct;

namespace Catalog.Application;

/// <summary>
/// Public Fusion compute service for the Catalog module. Replaces the
/// per-feature MediatR <c>IRequestHandler</c>s with a single intercepted
/// service.
/// </summary>
public interface ICatalogService : IComputeService
{
    [CommandHandler] Task<ProductDto> CreateProduct(CreateProductCommand command, CancellationToken ct = default);

    [ComputeMethod] Task<IReadOnlyList<ProductDto>> GetProducts(int page = 1, int pageSize = 20, CancellationToken ct = default);
}
