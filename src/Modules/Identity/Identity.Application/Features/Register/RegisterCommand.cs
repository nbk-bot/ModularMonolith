using ActualLab.CommandR;
using Identity.Application.Contracts;

namespace Identity.Application.Features.Register;

public sealed record RegisterCommand(string Email, string Password, string? FullName) : ICommand<UserDto>;
