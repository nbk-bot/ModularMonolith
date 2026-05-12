using Identity.Application.Features.ResetPassword;
using Identity.Domain;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Identity.Infrastructure.Features.ResetPassword;

internal sealed class ResetPasswordCommandHandler(UserManager<ApplicationUser> users)
    : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email)
            ?? throw new InvalidOperationException("User not found");

        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
