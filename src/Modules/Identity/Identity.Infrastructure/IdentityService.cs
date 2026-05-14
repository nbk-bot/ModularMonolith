using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Authentication;
using Identity.Application;
using Identity.Application.Abstractions;
using Identity.Application.Contracts;
using Identity.Application.Features.ConfirmEmail;
using Identity.Application.Features.ForgotPassword;
using Identity.Application.Features.Login;
using Identity.Application.Features.Logout;
using Identity.Application.Features.Refresh;
using Identity.Application.Features.Register;
using Identity.Application.Features.ResetPassword;
using Identity.Application.Mappers;
using Identity.Domain;
using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure;

/// <summary>
/// Single Fusion compute service that subsumes the seven former MediatR
/// command handlers (Register/Login/Refresh/Forgot/Reset/Confirm/Logout) plus
/// the <c>GetCurrentUser</c> compute method. All methods are <c>virtual</c>
/// so Fusion's interceptor can wrap them — the open-generic
/// <see cref="BuildingBlocks.Infrastructure.Commands.FluentValidationCommandHandler{TCommand}"/>
/// filter still runs first thanks to the <c>CommandR</c> pipeline.
/// Registered as singleton (Fusion's default) so per-call scoped dependencies
/// (UserManager, SignInManager, DbContext, ITokenService) must be resolved
/// through IServiceScopeFactory.
/// </summary>
public class IdentityService(
    IServiceScopeFactory scopeFactory,
    ISmsSender sms,
    IOptions<JwtOptions> jwtOpts) : IIdentityService
{
    public virtual async Task<UserDto> Register(RegisterCommand command, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = command.Email,
            Email = command.Email,
            FullName = command.FullName,
        };
        var result = await users.CreateAsync(user, command.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        return user.ToDto(roles: []);
    }

    public virtual async Task<AuthTokens> Login(LoginCommand command, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var user = await users.FindByEmailAsync(command.Email)
            ?? throw new UnauthorizedAccessException("Invalid credentials");

        var check = await signIn.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: true);
        if (!check.Succeeded) throw new UnauthorizedAccessException("Invalid credentials");

        var roles = await users.GetRolesAsync(user);
        var (access, expiresAt) = tokens.CreateAccessToken(user.Id, user.Email!,
            roles.Select(r => new System.Security.Claims.Claim("role", r)));
        var refresh = tokens.CreateRefreshToken();

        user.RefreshToken = refresh;
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(jwtOpts.Value.RefreshTokenDays);
        await db.SaveChangesAsync(ct);

        return new AuthTokens(access, refresh, expiresAt);
    }

    public virtual async Task<AuthTokens> Refresh(RefreshTokenCommand command, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var user = await db.Users.SingleOrDefaultAsync(u => u.RefreshToken == command.RefreshToken, ct)
            ?? throw new UnauthorizedAccessException("Invalid refresh token");

        if (user.RefreshTokenExpiresAt is null || user.RefreshTokenExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expired");

        var (access, expiresAt) = tokens.CreateAccessToken(user.Id, user.Email!, []);
        var refresh = tokens.CreateRefreshToken();

        user.RefreshToken = refresh;
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(jwtOpts.Value.RefreshTokenDays);
        await db.SaveChangesAsync(ct);

        return new AuthTokens(access, refresh, expiresAt);
    }

    public virtual async Task ForgotPassword(ForgotPasswordCommand command, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await users.FindByEmailAsync(command.Email)
            ?? throw new InvalidOperationException("User not found");

        if (string.IsNullOrWhiteSpace(user.PhoneNumber))
            throw new InvalidOperationException("Phone number not set");

        var token = await users.GeneratePasswordResetTokenAsync(user);
        await sms.SendAsync(user.PhoneNumber, $"Password reset code: {token}", ct);
    }

    public virtual async Task ResetPassword(ResetPasswordCommand command, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await users.FindByEmailAsync(command.Email)
            ?? throw new InvalidOperationException("User not found");

        var result = await users.ResetPasswordAsync(user, command.Token, command.NewPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    public virtual async Task ConfirmEmail(ConfirmEmailCommand command, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await users.FindByIdAsync(command.UserId)
            ?? throw new InvalidOperationException("User not found");

        var result = await users.ConfirmEmailAsync(user, command.Token);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    public virtual async Task Logout(LogoutCommand command, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, ct)
            ?? throw new InvalidOperationException("User not found");

        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;
        await db.SaveChangesAsync(ct);
    }

    public virtual async Task<UserDto?> GetCurrentUser(Guid userId, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return null;
        var roles = await users.GetRolesAsync(user);
        return user.ToDto(roles.ToArray());
    }
}
