using Identity.Application.Contracts;
using MediatR;

namespace Identity.Application.Features.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<AuthTokens>;
