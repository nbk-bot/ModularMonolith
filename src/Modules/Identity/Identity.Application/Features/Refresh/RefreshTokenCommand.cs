using Identity.Application.Contracts;
using MediatR;

namespace Identity.Application.Features.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthTokens>;
