using Catalog.Application.Features.CreateProduct;
using Catalog.Application.IntegrationEvents;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Catalog.Infrastructure.Tests;

/// <summary>
/// Unit tests target the underlying virtual methods on <see cref="CatalogService"/>
/// directly. We deliberately skip Fusion proxy wiring (CommandR pipeline,
/// FluentValidation filter, [ComputeMethod] interception) — those are integration
/// concerns. Here we exercise business logic against EF Core in-memory + a mocked
/// <see cref="IPublishEndpoint"/>.
/// </summary>
public class CatalogServiceTests
{
    private static CatalogDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CatalogDbContext(options);
    }

    [Fact]
    public async Task CreateProduct_PersistsToDb_AndPublishesIntegrationEvent()
    {
        await using var db = NewDb();
        var bus = new Mock<IPublishEndpoint>();
        var sut = new CatalogService(db, bus.Object);

        var dto = await sut.CreateProduct(new CreateProductCommand("Phone", 199.99m, 5, "desc"));

        dto.Name.Should().Be("Phone");
        dto.Price.Should().Be(199.99m);

        var persisted = await db.Products.SingleAsync();
        persisted.Name.Should().Be("Phone");
        persisted.Id.Should().Be(dto.Id);

        bus.Verify(b => b.Publish(
            It.Is<ProductCreatedIntegrationEvent>(e => e.ProductId == dto.Id && e.Name == "Phone" && e.Price == 199.99m),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetProducts_ReturnsPaginatedDescending()
    {
        await using var db = NewDb();
        var bus = new Mock<IPublishEndpoint>();
        var sut = new CatalogService(db, bus.Object);

        await sut.CreateProduct(new CreateProductCommand("A", 10m, 1, null));
        await Task.Delay(5);
        await sut.CreateProduct(new CreateProductCommand("B", 20m, 1, null));
        await Task.Delay(5);
        await sut.CreateProduct(new CreateProductCommand("C", 30m, 1, null));

        var page1 = await sut.GetProducts(1, 2);
        page1.Should().HaveCount(2);
        page1[0].Name.Should().Be("C");
        page1[1].Name.Should().Be("B");

        var page2 = await sut.GetProducts(2, 2);
        page2.Should().ContainSingle().Which.Name.Should().Be("A");
    }
}
