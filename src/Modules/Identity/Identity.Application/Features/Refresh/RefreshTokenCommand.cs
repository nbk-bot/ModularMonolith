using ActualLab.CommandR;
using Identity.Application.Contracts;

namespace Identity.Application.Features.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<AuthTokens>;
