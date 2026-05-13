using ActualLab.CommandR;
using Identity.Application.Contracts;

namespace Identity.Application.Features.Login;

public sealed record LoginCommand(string Email, string Password) : ICommand<AuthTokens>;
