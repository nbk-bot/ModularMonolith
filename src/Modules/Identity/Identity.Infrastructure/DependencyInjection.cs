using ActualLab.Fusion;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using FluentValidation;
using Identity.Application;
using Identity.Application.Abstractions;
using Identity.Domain;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Postgres connection string missing");

        services.AddDbContext<IdentityDbContext>((sp, o) => o
            .UseNpgsql(conn, b => b.MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.DefaultSchema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<DomainEventDispatcherInterceptor>()));

        services
            .AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequireNonAlphanumeric = false;
                o.User.RequireUniqueEmail = true;
                o.SignIn.RequireConfirmedEmail = false;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                o.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();

        // Fusion compute service: replaces MediatR per-feature handler scan.
        // CommandR routes [CommandHandler]-marked methods through the FluentValidation
        // open-generic filter registered in AddBuildingBlocks.
        services.AddFusion().AddService<IIdentityService, IdentityService>();

        // FluentValidation discovers Identity.Application validators here.
        services.AddValidatorsFromAssembly(typeof(IIdentityService).Assembly);

        return services;
    }
}
