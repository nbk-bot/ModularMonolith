using MediatR;

namespace Identity.Application.Features.Logout;

public sealed record LogoutCommand(Guid UserId) : IRequest;
