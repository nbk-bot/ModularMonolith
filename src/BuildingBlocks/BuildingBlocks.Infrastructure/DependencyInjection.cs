using System.Text;
using ActualLab.CommandR;
using ActualLab.Fusion;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Authentication;
using BuildingBlocks.Infrastructure.Caching;
using BuildingBlocks.Infrastructure.Commands;
using BuildingBlocks.Infrastructure.Messaging;
using BuildingBlocks.Infrastructure.Persistence;
using LoggerBot;
using MassTransit;
using Messager.EskizUz;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace BuildingBlocks.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Composition root for cross-cutting infrastructure: Fusion + CommandR,
    /// FluentValidation command filter, JWT, Redis cache, MassTransit RabbitMQ
    /// outbox host, health checks, env-based CORS, Eskiz SMS, LoggerBot,
    /// OpenTelemetry tracing + metrics.
    /// </summary>
    public static IServiceCollection AddBuildingBlocks(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        // ActualLab.Fusion + CommandR: replaces MediatR.
        // Each module's IXxxService : IComputeService is registered in its own AddXxxModule.
        var fusion = services.AddFusion();
        var commander = fusion.Commander;

        // Open-generic FluentValidation filter — runs before every business handler
        // (Priority = 1_000_000, IsFilter = true). FluentValidation discovers IValidator<>
        // instances per-module via AddValidatorsFromAssembly there.
        services.AddTransient(typeof(ICommandHandler<>), typeof(FluentValidationCommandHandler<>));
        commander.AddHandlers(typeof(FluentValidationCommandHandler<>));

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

        // Env-based CORS. Production MUST configure Cors:AllowedOrigins.
        var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        services.AddCors(o => o.AddPolicy("DefaultCors", p =>
        {
            if (allowedOrigins.Length > 0)
            {
                p.WithOrigins(allowedOrigins)
                    .AllowCredentials()
                    .AllowAnyHeader()
                    .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH");
            }
            else if (env.IsDevelopment())
            {
                p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            }
            else
            {
                throw new InvalidOperationException(
                    "Cors:AllowedOrigins is empty in a non-development environment. " +
                    "Configure the allowed origins list before starting.");
            }
        }));

        // OpenTelemetry tracing + metrics with OTLP exporter (default: http://localhost:4317).
        var otlpEndpoint = config["Otel:ExporterEndpoint"] ?? "http://localhost:4317";
        var serviceName = config["Otel:ServiceName"] ?? "ModularMonolith";
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

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
