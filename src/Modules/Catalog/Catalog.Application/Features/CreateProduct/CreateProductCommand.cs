using Catalog.Application.Contracts;
using FluentValidation;
using MediatR;

namespace Catalog.Application.Features.CreateProduct;

public sealed record CreateProductCommand(string Name, decimal Price, int Stock, string? Description)
    : IRequest<ProductDto>;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
    }
}
