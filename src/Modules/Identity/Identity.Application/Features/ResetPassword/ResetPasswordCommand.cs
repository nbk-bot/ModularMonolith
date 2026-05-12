using MediatR;

namespace Identity.Application.Features.ResetPassword;

public sealed record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest;
