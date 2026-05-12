using Identity.Application.Features.ConfirmEmail;
using Identity.Domain;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Identity.Infrastructure.Features.ConfirmEmail;

internal sealed class ConfirmEmailCommandHandler(UserManager<ApplicationUser> users)
    : IRequestHandler<ConfirmEmailCommand>
{
    public async Task Handle(ConfirmEmailCommand request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(request.UserId)
            ?? throw new InvalidOperationException("User not found");

        var result = await users.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
