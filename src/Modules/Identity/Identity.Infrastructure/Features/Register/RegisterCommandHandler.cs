using Identity.Application.Contracts;
using Identity.Application.Features.Register;
using Identity.Application.Mappers;
using Identity.Domain;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Identity.Infrastructure.Features.Register;

internal sealed class RegisterCommandHandler(UserManager<ApplicationUser> users)
    : IRequestHandler<RegisterCommand, UserDto>
{
    public async Task<UserDto> Handle(RegisterCommand request, CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
        };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        return user.ToDto(roles: []);
    }
}
