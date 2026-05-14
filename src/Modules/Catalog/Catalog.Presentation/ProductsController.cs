using ActualLab.CommandR;
using Catalog.Application;
using Catalog.Application.Contracts;
using Catalog.Application.Features.CreateProduct;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Presentation;

[ApiController]
[Route("api/products")]
[Authorize]
public sealed class ProductsController(ICatalogService catalog, ICommander commander) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await catalog.GetProducts(page, pageSize, ct));

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest req, CancellationToken ct)
        => Ok(await commander.Call(new CreateProductCommand(req.Name, req.Price, req.Stock, req.Description), ct));

    [HttpGet("export.xlsx")]
    [AllowAnonymous]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        // Big page size so we get every product in one shot — for very large catalogs this should be replaced with a stream/paged export.
        var items = await catalog.GetProducts(1, 100_000, ct);
        var bytes = ProductsExcelExporter.ToExcel(items);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "products.xlsx");
    }
}
