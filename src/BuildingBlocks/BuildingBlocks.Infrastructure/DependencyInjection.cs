using System.Reflection;
using System.Text;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Infrastructure.Authentication;
using BuildingBlocks.Infrastructure.Caching;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace BuildingBlocks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBuildingBlocks(
        this IServiceCollection services,
        IConfiguration config,
        params Assembly[] applicationAssemblies)
    {
        services.AddMediatR(c =>
        {
            c.RegisterServicesFromAssemblies(applicationAssemblies);
            c.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssemblies(applicationAssemblies);

        var redisConn = config.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConn));
        services.AddSingleton<ICacheService, RedisCacheService>();

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        var jwt = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                };
            });
        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration config,
        Action<IBusRegistrationConfigurator>? extra = null)
    {
        services.AddMassTransit(x =>
        {
            extra?.Invoke(x);
            x.UsingRabbitMq((context, cfg) =>
            {
                var host = config.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672";
                cfg.Host(new Uri(host));
                cfg.ConfigureEndpoints(context);
            });
        });
        return services;
    }
}
