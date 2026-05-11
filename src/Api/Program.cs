using BuildingBlocks.Infrastructure;
using Catalog.Application.Features.CreateProduct;
using Catalog.Infrastructure;
using Identity.Application.Features.Register;
using Identity.Infrastructure;
using Identity.Presentation.GraphQL;
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

    builder.Services.AddBuildingBlocks(cfg,
        typeof(RegisterCommand).Assembly,
        typeof(CreateProductCommand).Assembly);

    builder.Services.AddIdentityModule(cfg);
    builder.Services.AddCatalogModule(cfg);

    builder.Services.AddMessaging(cfg);

    builder.Services
        .AddGraphQLServer()
        .AddAuthorization()
        .AddQueryType(d => d.Name("Query"))
        .AddTypeExtension<IdentityQueries>();

    builder.Services.AddGrpc();
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    if (app.Environment.IsDevelopment()) app.MapOpenApi();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapGraphQL("/graphql");

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
