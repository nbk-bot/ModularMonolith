using System.Reflection;
using System.Text;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Behaviors;
using BuildingBlocks.Infrastructure.Authentication;
using BuildingBlocks.Infrastructure.Caching;
using BuildingBlocks.Infrastructure.Messaging;
using BuildingBlocks.Infrastructure.Persistence;
using FluentValidation;
using LoggerBot;
using MassTransit;
using MediatR;
using Messager.EskizUz;
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
        // MediatR scans the supplied Application assemblies for IRequestHandler,
        // INotificationHandler (domain events) and IPipelineBehavior implementations.
        services.AddMediatR(c =>
        {
            c.RegisterServicesFromAssemblies(applicationAssemblies);
            c.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssemblies(applicationAssemblies);

        // Domain event dispatcher attached to every DbContext via interceptor (see BaseDbContext / IdentityDbContext registrations).
        services.AddSingleton<DomainEventDispatcherInterceptor>();

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

        services.AddLoggerBot();

        services.Configure<EskizOptions>(config.GetSection(EskizOptions.SectionName));
        services.AddSingleton(sp =>
        {
            var opts = config.GetSection(EskizOptions.SectionName).Get<EskizOptions>() ?? new EskizOptions();
            return new MessagerAgent(opts.Email, opts.SecretKey);
        });
        services.AddSingleton<ISmsSender, EskizSmsSender>();

        var pgConn = config.GetConnectionString("Postgres")
            ?? "Host=localhost;Port=5432;Database=modmono;Username=postgres;Password=postgres";
        var rabbitConn = config.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672";

        // RabbitMQ.Client v7 dropped the simple "connection string" overload from the health-check package,
        // so we register a singleton IConnection factory bound to the same URI MassTransit uses.
        services.AddSingleton<RabbitMQ.Client.IConnection>(_ =>
        {
            var factory = new RabbitMQ.Client.ConnectionFactory { Uri = new Uri(rabbitConn) };
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        services.AddHealthChecks()
            .AddNpgSql(pgConn, name: "postgres")
            .AddRedis(redisConn, name: "redis")
            .AddRabbitMQ(name: "rabbitmq");

        // TODO: tighten CORS in production — allow specific origins/headers/methods per environment.
        services.AddCors(o => o.AddPolicy("DefaultCors", p => p
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()));

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
