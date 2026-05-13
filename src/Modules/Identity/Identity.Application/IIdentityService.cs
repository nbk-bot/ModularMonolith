using ActualLab.CommandR;
using ActualLab.CommandR.Configuration;
using ActualLab.Fusion;
using Identity.Application.Contracts;
using Identity.Application.Features.ConfirmEmail;
using Identity.Application.Features.ForgotPassword;
using Identity.Application.Features.Login;
using Identity.Application.Features.Logout;
using Identity.Application.Features.Refresh;
using Identity.Application.Features.Register;
using Identity.Application.Features.ResetPassword;

namespace Identity.Application;

/// <summary>
/// Public Fusion compute service for the Identity module. Replaces the
/// per-feature MediatR <c>IRequestHandler</c>s with a single intercepted
/// service. <see cref="CommandHandlerAttribute"/> methods are routed via
/// the Fusion <c>ICommander</c>; <see cref="ComputeMethodAttribute"/> methods
/// produce reactive <c>Computed&lt;T&gt;</c> results.
/// </summary>
public interface IIdentityService : IComputeService
{
    [CommandHandler] Task<UserDto> Register(RegisterCommand command, CancellationToken ct = default);
    [CommandHandler] Task<AuthTokens> Login(LoginCommand command, CancellationToken ct = default);
    [CommandHandler] Task<AuthTokens> Refresh(RefreshTokenCommand command, CancellationToken ct = default);
    [CommandHandler] Task ForgotPassword(ForgotPasswordCommand command, CancellationToken ct = default);
    [CommandHandler] Task ResetPassword(ResetPasswordCommand command, CancellationToken ct = default);
    [CommandHandler] Task ConfirmEmail(ConfirmEmailCommand command, CancellationToken ct = default);
    [CommandHandler] Task Logout(LogoutCommand command, CancellationToken ct = default);

    [ComputeMethod] Task<UserDto?> GetCurrentUser(Guid userId, CancellationToken ct = default);
}
