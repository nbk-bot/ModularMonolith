using MediatR;

namespace Identity.Application.Features.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : IRequest;
