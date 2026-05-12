using BuildingBlocks.Application.Abstractions;
using Identity.Application.Features.ForgotPassword;
using Identity.Domain;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Identity.Infrastructure.Features.ForgotPassword;

internal sealed class ForgotPasswordCommandHandler(
    UserManager<ApplicationUser> users,
    ISmsSender sms) : IRequestHandler<ForgotPasswordCommand>
{
    public async Task Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email)
            ?? throw new InvalidOperationException("User not found");

        if (string.IsNullOrWhiteSpace(user.PhoneNumber))
            throw new InvalidOperationException("Phone number not set");

        var token = await users.GeneratePasswordResetTokenAsync(user);
        await sms.SendAsync(user.PhoneNumber, $"Password reset code: {token}", ct);
    }
}
