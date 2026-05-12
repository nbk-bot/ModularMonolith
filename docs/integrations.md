# Integrations

Barcha cross-cutting infrastructure'lar `BuildingBlocks.Infrastructure`'da o'rnatiladi va modullar/Api ulardan foydalanadi.

## EF Core 10 + Postgres + snake_case

| Paket | Versiya |
|---|---|
| `Microsoft.EntityFrameworkCore` | 10.0.1 |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.0 |
| `EFCore.NamingConventions` | 10.0.0 |
| `Dapper` | 2.1.66 |

Har bir DbContext registratsiyasida:

```csharp
services.AddDbContext<CatalogDbContext>(o => o
    .UseNpgsql(conn, b => b.MigrationsHistoryTable("__ef_migrations_history", CatalogDbContext.DefaultSchema))
    .UseSnakeCaseNamingConvention());
```

`UseSnakeCaseNamingConvention()` natijasi:
- `Products` DbSet → `catalog.products` table
- `CreatedAt` property → `created_at` column
- `Product` entity → `products`
- Index/FK nomlari ham snake_case

Migrations history table'ni schema'ga bog'lash muhim: har bir modul o'z `__ef_migrations_history` jadvalini o'z schemasida saqlaydi, ular bir-biriga aralashmaydi.

**Dapper** — `BuildingBlocks.Infrastructure`'da reference. Murakkab SQL kerak bo'lsa (reporting, dynamic queries), DbContext orqali connection olinadi:

```csharp
await using var conn = db.Database.GetDbConnection();
var rows = await conn.QueryAsync<Foo>("SELECT ... FROM ...");
```

## Redis (caching)

`StackExchange.Redis` 2.9.11 + `Microsoft.Extensions.Caching.StackExchangeRedis` 10.0.0.

`BuildingBlocks.Infrastructure/DependencyInjection.cs` ichida:

```csharp
var redisConn = config.GetConnectionString("Redis") ?? "localhost:6379";
services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConn));
services.AddSingleton<ICacheService, RedisCacheService>();
```

`ICacheService` (BuildingBlocks.Application) JSON serialization bilan:

```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}
```

Handler'larda DI orqali `ICacheService` injektsiya, ishlatilishi:

```csharp
var cached = await cache.GetAsync<ProductDto>($"product:{id}", ct);
if (cached is not null) return cached;
// ... DB'dan o'qish ...
await cache.SetAsync($"product:{id}", dto, TimeSpan.FromMinutes(10), ct);
```

## RabbitMQ + MassTransit

`MassTransit` 8.5.4 + `MassTransit.RabbitMQ` 8.5.4.

`AddMessaging` extension:

```csharp
public static IServiceCollection AddMessaging(this IServiceCollection services,
    IConfiguration config, Action<IBusRegistrationConfigurator>? extra = null)
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
```

Publish:

```csharp
internal sealed class CreateProductCommandHandler(CatalogDbContext db, IPublishEndpoint bus)
    : IRequestHandler<CreateProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(...)
    {
        // ...
        await bus.Publish(new ProductCreatedIntegrationEvent(...), ct);
        // ...
    }
}
```

Consume (boshqa modulda):

```csharp
public sealed class ProductCreatedConsumer : IConsumer<ProductCreatedIntegrationEvent>
{
    public Task Consume(ConsumeContext<ProductCreatedIntegrationEvent> context) { ... }
}

// Program.cs'da AddMessaging chaqirig'ida:
builder.Services.AddMessaging(cfg, x => x.AddConsumer<ProductCreatedConsumer>());
```

## JWT Bearer + ASP.NET Identity

`Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.0 + `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.0.

`appsettings.json:Jwt` bo'limi:

```json
"Jwt": {
  "Issuer": "ModularMonolith",
  "Audience": "ModularMonolith.Api",
  "SigningKey": "min-32-belgi-strong-secret",
  "AccessTokenMinutes": 60,
  "RefreshTokenDays": 14
}
```

`AddBuildingBlocks`'da:

```csharp
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
```

Token tarkibi (`TokenService.cs`):

```
sub = userId
email = email
jti = guid (har token uchun unique)
role = ... (har bir rol uchun alohida claim)
```

Refresh token — `RandomNumberGenerator.GetBytes(64)` → base64. DB'da `ApplicationUser.RefreshToken` + `RefreshTokenExpiresAt`. Refresh endpoint loyihaga hali qo'shilmagan — tipik: `POST /api/auth/refresh` → eskini DB'da topish → expiry tekshirish → yangi access+refresh berish.

## GraphQL (HotChocolate)

`HotChocolate.AspNetCore` 14.3.0 + `HotChocolate.AspNetCore.Authorization`.

`Program.cs`:

```csharp
builder.Services
    .AddGraphQLServer()
    .AddAuthorization()
    .AddQueryType(d => d.Name("Query"))
    .AddTypeExtension<IdentityQueries>();
```

`IdentityQueries`:

```csharp
[ExtendObjectType("Query")]
public sealed class IdentityQueries
{
    [Authorize]
    public MeDto Me(ClaimsPrincipal user) => new(
        Id: user.FindFirst("sub")?.Value ?? "",
        Email: user.FindFirst("email")?.Value ?? "",
        Roles: user.FindAll("role").Select(c => c.Value).ToArray());
}
```

Pipeline: `MapGraphQL("/graphql")`. Banana Cake Pop browser IDE — `https://localhost:5001/graphql`.

Query namuna:

```graphql
query { me { id email roles } }
```

Header: `Authorization: Bearer <jwt>`.

## gRPC

`Grpc.AspNetCore` 2.71.0 + `Grpc.Tools` 2.71.0 + `Google.Protobuf` 3.30.2.

`Grpc.Tools` har modul Presentation projektida — `.proto` fayllarni `<Protobuf Include="..." />` orqali qo'shsangiz bo'ladi.

`Program.cs`:

```csharp
builder.Services.AddGrpc();
// app.MapGrpcService<MyGrpcService>();
```

Hozircha boshlang'ich service'lar yo'q — `.proto`'larni qo'shib, generated server class'idan service yaratasiz.

## Serilog

`Serilog.AspNetCore` 9.0.0 + Console/File/Seq/EnvironmentEnricher sinklari.

Bootstrap logger (host hali ko'tarilmasdan):

```csharp
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
```

Host (full configured):

```csharp
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day));
```

`appsettings.json:Serilog` bo'limi minimum level + overridelarni boshqaradi:

```json
"Serilog": {
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

Seq sink — `docker-compose.yml` da `seq` service `5341` portda. `appsettings.json`'ga qo'shib:

```json
"Serilog": {
  "WriteTo": [
    { "Name": "Seq", "Args": { "serverUrl": "http://localhost:5341" } }
  ]
}
```

## Coravel

`Coravel` 6.0.2 — scheduling + queues + caching + event broadcasting.

`Program.cs`'da wired:

```csharp
builder.Services.AddScheduler();
builder.Services.AddQueue();
builder.Services.AddTransient<CleanupExpiredRefreshTokensInvocable>();
// ...
app.Services.UseScheduler(s => s.Schedule<CleanupExpiredRefreshTokensInvocable>().Daily());
```

Sample invocable: `BuildingBlocks.Infrastructure/Scheduling/CleanupExpiredRefreshTokensInvocable.cs` — kunda bir marta expired refresh token'larni `ApplicationUser` ustidan tozalaydi. BuildingBlocks Identity modulga referencelay olmagani uchun DbContext type ni runtime'da `AppDomain` orqali topadi. Ideal yechim — `BuildingBlocks.Application`'ga `IRefreshTokenStore` abstraction qo'shib, Identity infrastructure'da implementatsiya qilish (TODO).

## Health checks

`Microsoft.Extensions.Diagnostics.HealthChecks` (framework reference) + uchta provider:

| Paket | Versiya |
|---|---|
| `AspNetCore.HealthChecks.NpgSql` | 9.0.0 |
| `AspNetCore.HealthChecks.Redis` | 9.0.0 |
| `AspNetCore.HealthChecks.Rabbitmq` | 9.0.0 |

`AddBuildingBlocks` ichida `ConnectionStrings:Postgres / Redis / RabbitMQ` qiymatlarini o'qib registratsiya qiladi. RabbitMQ 9.x faqat `IConnection` factory'ni qabul qiladi — `RabbitMQ.Client.ConnectionFactory` singleton DI'ga registratsiya qilingan.

Endpoint: `app.MapHealthChecks("/healthz")`.

## CORS

`AddBuildingBlocks` ichida `DefaultCors` policy registratsiya qilingan — development uchun `AllowAnyOrigin/Header/Method`. `Program.cs`'da `app.UseCors("DefaultCors")` autentifikatsiyadan oldin keladi. TODO: production uchun explicit origins ro'yxati bilan tighten qilish.

## OpenAPI JWT bearer

`Microsoft.AspNetCore.OpenApi` 10.0.7 ishlatamiz (Swashbuckle emas). `BuildingBlocks.Infrastructure/OpenApi/BearerSecuritySchemeTransformer.cs` — `IOpenApiDocumentTransformer` implementatsiyasi `bearerAuth` (http/bearer/JWT) security scheme'ni qo'shadi va barcha operatsiyalarga default requirement sifatida qo'llaydi.

```csharp
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
```

`/openapi/v1.json` (faqat Development) endpoint'i shu sxema bilan to'lib chiqadi.

## MassTransit EF Outbox

`MassTransit.EntityFrameworkCore` 8.5.4 har bir module DbContext uchun outbox jadvallarini qo'shadi (`AddEntityFrameworkOutbox<TDbContext>`). `Program.cs`'dagi `AddMessaging`'ga ikkala DbContext registratsiya qilingan:

```csharp
builder.Services.AddMessaging(cfg, x =>
{
    x.AddEntityFrameworkOutbox<IdentityDbContext>(o => { o.UsePostgres(); o.UseBusOutbox(); });
    x.AddEntityFrameworkOutbox<CatalogDbContext>(o =>  { o.UsePostgres(); o.UseBusOutbox(); });
});
```

Handler ichida `bus.Publish(...)` `SaveChangesAsync`'dan **oldin** chaqiriladi — MassTransit message'ni outbox jadvaliga oladi va broker'ga faqat transaction commit bo'lgandan keyin yuboradi (`CreateProductCommandHandler` namuna).

Outbox jadvalini yaratish uchun migration kerak — `OutboxMessage` / `OutboxState` / `InboxState` `IdentityDbContext.OnModelCreating` va `CatalogDbContext.OnModelCreating` ichidan `modelBuilder.AddOutboxStateEntity()` orqali model'ga qo'shilishi mumkin (kelajakda).

## Domain event dispatcher

`BuildingBlocks.Infrastructure/Persistence/DomainEventDispatcherInterceptor.cs` — `SaveChangesInterceptor`. SavingChangesAsync ichida `ChangeTracker` orqali tracked entity'lardan `IDomainEvent`'larni yig'ib oladi va `ClearDomainEvents()` chaqiradi; SavedChangesAsync ichida (transaction muvaffaqiyatli tugagandan keyin) har bir event'ni `IMediator.Publish` qiladi.

Interceptor singleton DI'da, ikkala module DbContext (`IdentityDbContext`, `CatalogDbContext`) registratsiyasida `AddInterceptors(...)` orqali ulanadi. `IdentityDbContext` `IdentityDbContext<...>`'dan inherit qilgani uchun `BaseDbContext`'ga o'tib bo'lmadi — interceptor pattern bu cheklovni hal qildi.

## Identity dev seed

`Identity.Infrastructure/Seeding/IdentitySeeder.cs` — `Admin` + `User` rollarini ta'minlaydi va birorta user bo'lmasa `admin@local.dev` / `Admin123!` admin user yaratadi. `Program.cs`'da faqat `app.Environment.IsDevelopment()` da chaqiriladi.

## OpenXML export endpoint

`Catalog.Presentation/ProductsExcelExporter.cs` — `IReadOnlyList<ProductDto>`'ni bitta worksheet (Id/Name/Price/Stock/CreatedAt) xlsx fayliga aylantiradi. `ProductsController.Export` — `GET /api/products/export.xlsx`, katta pageSize bilan `GetProductsQuery` yuboradi.

## gRPC sample

- `Catalog.Presentation/Protos/catalog.proto` — `service CatalogGrpc { rpc ListProducts(...) returns (...); }`.
- `Catalog.Presentation/Grpc/CatalogGrpcService.cs` — generated base'ni override qiladi, `ISender.Send(GetProductsQuery)` orqali MediatR'ga delegate qiladi.
- `Program.cs`'da `app.MapGrpcService<CatalogGrpcService>()`.

## DocumentFormat.OpenXml

`DocumentFormat.OpenXml` 3.3.0 — Excel/Word/PowerPoint fayllarni server tomonida yaratish.

Tipik foydalanish: alohida Reporting modul yaratib, `Reporting.Infrastructure`'da generatorlar yozish. Har bir entity uchun:

```csharp
public static byte[] ToExcel(IEnumerable<ProductDto> products)
{
    using var ms = new MemoryStream();
    using var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook);
    // ... worksheet/cells qo'shish ...
    return ms.ToArray();
}
```

Controller'da:

```csharp
[HttpGet("export.xlsx")]
public async Task<IActionResult> Export(CancellationToken ct)
{
    var list = await sender.Send(new GetProductsQuery(1, int.MaxValue), ct);
    var bytes = ProductsExcelExporter.ToExcel(list);
    return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "products.xlsx");
}
```

## MediatR + FluentValidation

`MediatR` 12.4.1 (so'nggi free version), `FluentValidation` 12.0.0.

`AddBuildingBlocks` har bir passed assembly'da handlerlarni va validatorlarni topib register qiladi:

```csharp
services.AddMediatR(c =>
{
    c.RegisterServicesFromAssemblies(applicationAssemblies);
    c.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
services.AddValidatorsFromAssemblies(applicationAssemblies);
```

`ValidationBehavior` — har MediatR request'dan oldin shu request type uchun barcha validatorlarni yuradi:

```csharp
var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct))))
    .SelectMany(r => r.Errors).Where(f => f is not null).ToList();
if (failures.Count > 0) throw new ValidationException(failures);
return await next();
```

## Riok.Mapperly

`Riok.Mapperly` 4.2.1 — source generator (runtime reflection yo'q, AOT-friendly).

Mapper class'i `[Mapper]` attribute bilan `static partial`:

```csharp
[Mapper]
public static partial class ProductMapper
{
    public static partial ProductDto ToDto(this Product entity);
    public static partial IReadOnlyList<ProductDto> ToDtoList(this IEnumerable<Product> entities);
}
```

Build paytida implementation generated bo'ladi. Mapping yo'q maydon bo'lsa warning (`RMG020`) chiqaradi — qaysi propertyni e'tibor qilmaslik kerak bo'lsa, atribut bilan boshqariladi.

## Known issue: HotChocolate.Language vulnerability

`HotChocolate.Language` 14.3.0 da GHSA-qr3m-xw4c-jqw3 (NU1904 warning). Build error emas, faqat ogohlantirish. Yangi patch chiqishi bilan version bump kerak.
