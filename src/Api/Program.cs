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
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Serilog;
using System.Threading.RateLimiting;

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

    builder.Services.AddBuildingBlocks(cfg, builder.Environment);

    // Rate limiting — "auth" policy applied to AuthController (5 req / 30s, no queue).
    builder.Services.AddRateLimiter(o =>
    {
        o.AddFixedWindowLimiter("auth", w =>
        {
            w.PermitLimit = 5;
            w.Window = TimeSpan.FromSeconds(30);
            w.QueueLimit = 0;
        });
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

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
    app.UseRateLimiter();
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

    // Dev-only auto-provision schema for every module DbContext, then seed
    // (Admin/User roles + default admin user). No EF migrations exist yet, so we
    // call IRelationalDatabaseCreator.CreateTablesAsync per context — Database.EnsureCreatedAsync
    // skips the second context when both share the same database. Switch to
    // MigrateAsync once migrations are added. Production runs provisioning out-of-band.
    if (app.Environment.IsDevelopment())
    {
        using (var scope = app.Services.CreateScope())
        {
            await EnsureSchemaAsync(scope.ServiceProvider.GetRequiredService<IdentityDbContext>());
            await EnsureSchemaAsync(scope.ServiceProvider.GetRequiredService<CatalogDbContext>());
        }
        await IdentitySeeder.SeedAsync(app.Services);

        static async Task EnsureSchemaAsync(DbContext db)
        {
            var creator = (RelationalDatabaseCreator)db.GetService<IDatabaseCreator>();
            if (!await creator.ExistsAsync()) await creator.CreateAsync();
            // HasTablesAsync returns true once ANY context has provisioned tables in the shared DB,
            // so we run CreateTables unconditionally and swallow "table already exists" — each
            // context's tables live in its own schema, so the first run for each is conflict-free.
            try { await creator.CreateTablesAsync(); }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P07") { /* duplicate_table */ }
        }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Enables WebApplicationFactory<Program> in integration tests.
public partial class Program;
