using Identity.Application.Contracts;
using Identity.Domain;
using Riok.Mapperly.Abstractions;

namespace Identity.Application.Mappers;

[Mapper]
public static partial class UserMapper
{
    public static partial UserDto ToDto(this ApplicationUser entity, IReadOnlyList<string> roles);
}
