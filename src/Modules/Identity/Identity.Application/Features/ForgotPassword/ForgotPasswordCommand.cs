using ActualLab.CommandR;

namespace Identity.Application.Features.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : ICommand;
