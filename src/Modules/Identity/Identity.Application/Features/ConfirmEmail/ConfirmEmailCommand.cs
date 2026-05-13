using ActualLab.CommandR;

namespace Identity.Application.Features.ConfirmEmail;

public sealed record ConfirmEmailCommand(string UserId, string Token) : ICommand;
