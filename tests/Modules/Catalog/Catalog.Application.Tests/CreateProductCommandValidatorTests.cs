using Catalog.Application.Features.CreateProduct;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace Catalog.Application.Tests;

public class CreateProductCommandValidatorTests
{
    private readonly CreateProductCommandValidator _sut = new();

    [Theory]
    [InlineData("Phone", 199.99, 10)]
    [InlineData("Laptop", 1500, 0)]
    [InlineData("Mouse", 9.5, 1000)]
    public void Should_Pass_For_Valid_Inputs(string name, decimal price, int stock)
    {
        var result = _sut.TestValidate(new CreateProductCommand(name, price, stock, null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_Fail_When_Name_Empty()
    {
        var result = _sut.TestValidate(new CreateProductCommand("", 10m, 1, null));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_Fail_When_Price_NonPositive(decimal price)
    {
        var result = _sut.TestValidate(new CreateProductCommand("X", price, 1, null));
        result.ShouldHaveValidationErrorFor(x => x.Price);
    }

    [Fact]
    public void Should_Fail_When_Stock_Negative()
    {
        var result = _sut.TestValidate(new CreateProductCommand("X", 10m, -1, null));
        result.ShouldHaveValidationErrorFor(x => x.Stock);
    }

    [Fact]
    public void Should_Fail_When_Name_TooLong()
    {
        var name = new string('a', 201);
        var result = _sut.TestValidate(new CreateProductCommand(name, 10m, 1, null));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}
