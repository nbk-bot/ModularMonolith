using Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Seeding;

/// <summary>
/// Idempotent dev seed: makes sure the <c>Admin</c> and <c>User</c> roles exist
/// and, when no users have been created yet, provisions a default admin
/// account (<c>admin@local.dev</c> / <c>Admin123!</c>) in the <c>Admin</c> role.
/// Wire this up only in <c>Development</c> — production data should never be
/// auto-seeded with a known password.
/// </summary>
public static class IdentitySeeder
{
    public const string AdminRole = "Admin";
    public const string UserRole = "User";
    public const string DefaultAdminEmail = "admin@local.dev";
    public const string DefaultAdminPassword = "Admin123!";

    public static async Task SeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(IdentitySeeder));
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { AdminRole, UserRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new ApplicationRole { Name = role, Description = $"{role} role" });
                if (!result.Succeeded)
                    logger.LogWarning("Failed to seed role {Role}: {Errors}", role, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        if (!userManager.Users.Any())
        {
            var admin = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = DefaultAdminEmail,
                Email = DefaultAdminEmail,
                EmailConfirmed = true,
                FullName = "Default Admin",
            };
            var created = await userManager.CreateAsync(admin, DefaultAdminPassword);
            if (created.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, AdminRole);
                logger.LogInformation("Seeded default admin user {Email}.", DefaultAdminEmail);
            }
            else
            {
                logger.LogWarning("Failed to seed default admin: {Errors}", string.Join(", ", created.Errors.Select(e => e.Description)));
            }
        }
    }
}
