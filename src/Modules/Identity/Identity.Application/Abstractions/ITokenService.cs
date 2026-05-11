using System.Security.Claims;

namespace Identity.Application.Abstractions;

public interface ITokenService
{
    (string AccessToken, DateTime ExpiresAt) CreateAccessToken(Guid userId, string email, IEnumerable<Claim> extraClaims);
    string CreateRefreshToken();
}
