using Catalog.Application.Contracts;
using Catalog.Domain;
using Riok.Mapperly.Abstractions;

namespace Catalog.Application.Mappers;

[Mapper]
public static partial class ProductMapper
{
    public static partial ProductDto ToDto(this Product entity);
    public static partial IReadOnlyList<ProductDto> ToDtoList(this IEnumerable<Product> entities);
}
