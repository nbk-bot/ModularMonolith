using ActualLab.CommandR;

namespace Identity.Application.Features.Logout;

public sealed record LogoutCommand(Guid UserId) : ICommand;
