using Catalog.Domain;
using FluentAssertions;

namespace Catalog.Domain.Tests;

public class ProductTests
{
    [Fact]
    public void Create_ShouldRaiseProductCreatedEvent_AndSetFields()
    {
        var product = Product.Create("Phone", 199.99m, 10, "desc");

        product.Name.Should().Be("Phone");
        product.Price.Should().Be(199.99m);
        product.Stock.Should().Be(10);
        product.Description.Should().Be("desc");
        product.Id.Should().NotBe(Guid.Empty);
        product.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        product.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProductCreated>()
            .Which.ProductId.Should().Be(product.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100.5)]
    public void Create_ShouldThrow_WhenPriceNonPositive(decimal price)
    {
        var act = () => Product.Create("Phone", price, 10);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdatePrice_ShouldRaiseProductPriceChanged_AndSetNewPrice()
    {
        var product = Product.Create("Phone", 100m, 5);
        product.ClearDomainEvents();

        product.UpdatePrice(250m);

        product.Price.Should().Be(250m);
        product.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProductPriceChanged>()
            .Which.NewPrice.Should().Be(250m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void UpdatePrice_ShouldThrow_WhenPriceNonPositive(decimal price)
    {
        var product = Product.Create("Phone", 100m, 5);
        var act = () => product.UpdatePrice(price);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Restock_ShouldIncreaseStock()
    {
        var product = Product.Create("Phone", 100m, 5);
        product.Restock(7);
        product.Stock.Should().Be(12);
    }
}
