using Identity.Application.Contracts;
using MediatR;

namespace Identity.Application.Features.Register;

public sealed record RegisterCommand(string Email, string Password, string? FullName) : IRequest<UserDto>;
