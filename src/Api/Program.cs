using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.OpenApi;
using BuildingBlocks.Infrastructure.Scheduling;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using Catalog.Presentation.Grpc;
using Coravel;
using GlobalErrorHandler;
using Identity.Infrastructure;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Seeding;
using Identity.Presentation.GraphQL;
using MassTransit;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .WriteTo.Console()
        .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day));

    var cfg = builder.Configuration;

    builder.Services.AddBuildingBlocks(cfg);

    builder.Services.AddIdentityModule(cfg);
    builder.Services.AddCatalogModule(cfg);

    // MassTransit + EF Core outbox per module DbContext, so Publish() inside a handler
    // is captured into the outbox table in the same transaction as the entity changes.
    builder.Services.AddMessaging(cfg, x =>
    {
        x.AddEntityFrameworkOutbox<IdentityDbContext>(o =>
        {
            o.UsePostgres();
            o.UseBusOutbox();
        });
        x.AddEntityFrameworkOutbox<CatalogDbContext>(o =>
        {
            o.UsePostgres();
            o.UseBusOutbox();
        });
    });

    builder.Services
        .AddGraphQLServer()
        .AddAuthorization()
        .AddQueryType(d => d.Name("Query"))
        .AddTypeExtension<IdentityQueries>();

    builder.Services.AddGrpc();
    builder.Services.AddControllers();
    builder.Services.AddOpenApi(o =>
    {
        // Adds bearerAuth (http/bearer/JWT) to the generated OpenAPI document and applies it to all operations.
        o.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    });

    // Coravel scheduler + queue.
    builder.Services.AddScheduler();
    builder.Services.AddQueue();
    builder.Services.AddTransient<CleanupExpiredRefreshTokensInvocable>();

    var app = builder.Build();

    app.UseErrorHandler();
    app.UseSerilogRequestLogging();
    if (app.Environment.IsDevelopment()) app.MapOpenApi();

    // CORS must run before authentication/authorization.
    app.UseCors("DefaultCors");
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapGraphQL("/graphql");
    app.MapGrpcService<CatalogGrpcService>();
    app.MapHealthChecks("/healthz");

    // Sample Coravel schedule — runs daily, cleans up expired refresh tokens.
    app.Services.UseScheduler(s =>
    {
        s.Schedule<CleanupExpiredRefreshTokensInvocable>().Daily();
    });

    // Dev-only data seeding (Admin/User roles + default admin user).
    if (app.Environment.IsDevelopment())
    {
        await IdentitySeeder.SeedAsync(app.Services);
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
