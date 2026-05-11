using HotChocolate.Authorization;
using System.Security.Claims;

namespace Identity.Presentation.GraphQL;

[ExtendObjectType("Query")]
public sealed class IdentityQueries
{
    [Authorize]
    public MeDto Me(ClaimsPrincipal user) => new(
        Id: user.FindFirst("sub")?.Value ?? "",
        Email: user.FindFirst("email")?.Value ?? "",
        Roles: user.FindAll("role").Select(c => c.Value).ToArray());
}

public sealed record MeDto(string Id, string Email, IReadOnlyList<string> Roles);
