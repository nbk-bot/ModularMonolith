using Catalog.Application.Contracts;
using Catalog.Application.Features.CreateProduct;
using Catalog.Application.Features.GetProducts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Presentation;

[ApiController]
[Route("api/products")]
[Authorize]
public sealed class ProductsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await sender.Send(new GetProductsQuery(page, pageSize), ct));

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest req, CancellationToken ct)
        => Ok(await sender.Send(new CreateProductCommand(req.Name, req.Price, req.Stock, req.Description), ct));
}
