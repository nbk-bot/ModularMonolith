namespace Catalog.Application.Contracts;

public sealed record ProductDto(Guid Id, string Name, string? Description, decimal Price, int Stock, DateTime CreatedAt);

public sealed record CreateProductRequest(string Name, decimal Price, int Stock, string? Description);
public sealed record UpdatePriceRequest(decimal Price);
