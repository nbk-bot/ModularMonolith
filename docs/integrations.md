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
services.AddDbContext<CatalogDbContext>((sp, o) => o
    .UseNpgsql(conn, b => b.MigrationsHistoryTable("__ef_migrations_history", CatalogDbContext.DefaultSchema))
    .UseSnakeCaseNamingConvention()
    .AddInterceptors(sp.GetRequiredService<DomainEventDispatcherInterceptor>()));
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

Compute service metodlarida DI orqali `ICacheService` injektsiya:

```csharp
var cached = await cache.GetAsync<ProductDto>($"product:{id}", ct);
if (cached is not null) return cached;
// ... DB'dan o'qish ...
await cache.SetAsync($"product:{id}", dto, TimeSpan.FromMinutes(10), ct);
```

Eslatma: Fusion `[ComputeMethod]` allaqachon in-process reactive cache beradi (invalidation orqali yangilanadi) — Redis cross-instance distributed cache uchun mo'ljallangan.

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

Publish — Fusion compute service ichidan to'g'ridan-to'g'ri:

```csharp
public class CatalogService(CatalogDbContext db, IPublishEndpoint bus) : ICatalogService
{
    public virtual async Task<ProductDto> CreateProduct(CreateProductCommand command, CancellationToken ct = default)
    {
        var product = Product.Create(command.Name, command.Price, command.Stock, command.Description);
        db.Products.Add(product);

        // EF Core outbox aktiv — Publish() outbox jadvaliga oladi,
        // brokerga faqat SaveChangesAsync transactioni commit bo'lgandan keyin yuboriladi.
        await bus.Publish(new ProductCreatedIntegrationEvent(product.Id, product.Name, product.Price), ct);

        await db.SaveChangesAsync(ct);
        return product.ToDto();
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

Refresh token — `RandomNumberGenerator.GetBytes(64)` → base64. DB'da `ApplicationUser.RefreshToken` + `RefreshTokenExpiresAt`. `POST /api/auth/refresh` endpointi `IIdentityService.Refresh(RefreshTokenCommand)` ni chaqiradi → eskini DB'da topadi → expiry tekshiradi → yangi access+refresh beradi.

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
app.MapGrpcService<CatalogGrpcService>();
```

Misol service — Fusion compute service'ni to'g'ridan-to'g'ri chaqiradi:

```csharp
public sealed class CatalogGrpcService(ICatalogService catalog) : CatalogGrpc.CatalogGrpcBase
{
    public override async Task<ListProductsResponse> ListProducts(ListProductsRequest req, ServerCallContext ctx)
    {
        var items = await catalog.GetProducts(req.Page, req.PageSize, ctx.CancellationToken);
        // ... map to proto response ...
    }
}
```

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

Sample invocable: `BuildingBlocks.Infrastructure/Scheduling/CleanupExpiredRefreshTokensInvocable.cs` — kunda bir marta expired refresh token'larni `ApplicationUser` ustidan tozalaydi. `BuildingBlocks.Application/Abstractions/IRefreshTokenStore` abstraction orqali ishlaydi — Identity infrastructure implementatsiyani (`RefreshTokenStore`) DI'ga registratsiya qiladi.

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

`AddBuildingBlocks` ichida `DefaultCors` policy env-based registratsiya qilingan. `Cors:AllowedOrigins` (string[]) qiymati:

- **Bo'sh emas** → `WithOrigins(...).AllowCredentials().AllowAnyHeader().WithMethods(GET/POST/PUT/DELETE/PATCH)`.
- **Bo'sh AND Development** → `AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()`.
- **Bo'sh AND non-Development** → `InvalidOperationException` startda otiladi.

`appsettings.Production.json`'da `Cors:AllowedOrigins` ro'yxati majburiy. `Program.cs`'da `app.UseCors("DefaultCors")` autentifikatsiyadan oldin keladi.

## Rate limiting

`Microsoft.AspNetCore.RateLimiting` (built-in net10) — `AuthController`'ga `[EnableRateLimiting("auth")]` attribute qo'yilgan. `Program.cs`:

```csharp
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
// ...
app.UseRateLimiter();   // UseAuthorization() dan oldin
```

## OpenTelemetry

Tracing + metrics OTLP exporter bilan. Paketlar:

| Paket | Versiya |
|---|---|
| `OpenTelemetry.Extensions.Hosting` | 1.10.0 |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.10.1 |
| `OpenTelemetry.Instrumentation.Http` | 1.10.0 |
| `OpenTelemetry.Instrumentation.EntityFrameworkCore` | 1.10.0-beta.1 |
| `OpenTelemetry.Instrumentation.Runtime` | 1.10.0 |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.10.0 |

`AddBuildingBlocks` ichida `Otel:ServiceName` + `Otel:ExporterEndpoint` (default `http://localhost:4317`) o'qiladi va tracer/meter quriladi. Collector / Jaeger / Tempo / Prometheus shu endpointga ulanadi.

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

Compute service ichida `bus.Publish(...)` `SaveChangesAsync`'dan **oldin** chaqiriladi — MassTransit message'ni outbox jadvaliga oladi va broker'ga faqat transaction commit bo'lgandan keyin yuboradi (`CatalogService.CreateProduct` namuna).

### Outbox model wiring

`IdentityDbContext` va `CatalogDbContext`'ning `OnModelCreating` metodlarida outbox entitylari modelga qo'shilgan (`MassTransit` namespace'idan extension'lar):

```csharp
modelBuilder.AddInboxStateEntity();
modelBuilder.AddOutboxMessageEntity();
modelBuilder.AddOutboxStateEntity();
```

### Migration yaratish

Har bir DbContext uchun migration ni alohida generate qiling — outbox jadvallari modul schema'da yaratiladi (`identity.*`, `catalog.*`):

```bash
# Identity
dotnet ef migrations add Outbox \
    --project src/Modules/Identity/Identity.Infrastructure \
    --startup-project src/Api \
    --context IdentityDbContext \
    --output-dir Persistence/Migrations

# Catalog
dotnet ef migrations add Outbox \
    --project src/Modules/Catalog/Catalog.Infrastructure \
    --startup-project src/Api \
    --context CatalogDbContext \
    --output-dir Persistence/Migrations
```

`dotnet ef database update`'dan keyin `inbox_state`, `outbox_message`, `outbox_state` jadvallari mos schema'da paydo bo'ladi.

## Domain event dispatcher

`BuildingBlocks.Infrastructure/Persistence/DomainEventDispatcherInterceptor.cs` — `SaveChangesInterceptor`. `SavingChangesAsync` ichida `ChangeTracker` orqali tracked entity'lardan `IDomainEvent`'larni yig'ib oladi va `ClearDomainEvents()` chaqiradi; `SavedChangesAsync` ichida (transaction muvaffaqiyatli tugagandan keyin) har bir event uchun DI'dan barcha `IDomainEventHandler<TEvent>` larni resolve qiladi va `HandleAsync` ni chaqiradi.

Interceptor singleton DI'da, ikkala module DbContext (`IdentityDbContext`, `CatalogDbContext`) registratsiyasida `AddInterceptors(...)` orqali ulanadi. `IdentityDbContext` `IdentityDbContext<...>`'dan inherit qilgani uchun `BaseDbContext`'ga o'tib bo'lmadi — interceptor pattern bu cheklovni hal qildi.

MediatR `INotification` o'rniga shu `IDomainEventHandler<T>` abstractioni ishlatiladi — endi `MediatR.IPublisher`/`IMediator` ga bog'liqlik yo'q.

## Identity dev seed

`Identity.Infrastructure/Seeding/IdentitySeeder.cs` — `Admin` + `User` rollarini ta'minlaydi va birorta user bo'lmasa `admin@local.dev` / `Admin123!` admin user yaratadi. `Program.cs`'da faqat `app.Environment.IsDevelopment()` da chaqiriladi.

## OpenXML export endpoint

`Catalog.Presentation/ProductsExcelExporter.cs` — `IReadOnlyList<ProductDto>`'ni bitta worksheet (Id/Name/Price/Stock/CreatedAt) xlsx fayliga aylantiradi. `ProductsController.Export` — `GET /api/products/export.xlsx`, katta `pageSize` bilan `ICatalogService.GetProducts(1, 100_000, ct)` chaqiradi.

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

Controller'da (compute service'ni to'g'ridan-to'g'ri chaqirib):

```csharp
[HttpGet("export.xlsx")]
public async Task<IActionResult> Export(CancellationToken ct)
{
    var list = await catalog.GetProducts(1, 100_000, ct);
    var bytes = ProductsExcelExporter.ToExcel(list);
    return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "products.xlsx");
}
```

## ActualLab.Fusion + CommandR + FluentValidation

`ActualLab.Fusion` 12.5.2 (Apache 2.0) — CQRS yo'naltirilgan reactive compute service framework. MediatR o'rnida ishlatamiz.

`AddBuildingBlocks`'da global wiring:

```csharp
var fusion = services.AddFusion();
var commander = fusion.Commander;

// Open-generic FluentValidation filter (Priority = 1_000_000, IsFilter = true)
services.AddTransient(typeof(ICommandHandler<>), typeof(FluentValidationCommandHandler<>));
commander.AddHandlers(typeof(FluentValidationCommandHandler<>));
```

Har modul'ning `AddXxxModule` ichida:

```csharp
services.AddFusion().AddService<IIdentityService, IdentityService>();   // proxy generation
services.AddValidatorsFromAssembly(typeof(IIdentityService).Assembly);  // validatorlarni topadi
```

### Command record

```csharp
public sealed record RegisterCommand(string Email, string Password, string? FullName)
    : ICommand<UserDto>;
```

### Compute service

Yagona public interface, `[CommandHandler]` va `[ComputeMethod]` attributelar bilan:

```csharp
public interface IIdentityService : IComputeService
{
    [CommandHandler] Task<UserDto> Register(RegisterCommand command, CancellationToken ct = default);
    [ComputeMethod]  Task<UserDto?> GetCurrentUser(Guid userId, CancellationToken ct = default);
}
```

Implementation Infrastructure layer'ida — metodlar **`virtual`** bo'lishi shart:

```csharp
public class IdentityService(UserManager<ApplicationUser> users, ITokenService tokens) : IIdentityService
{
    public virtual async Task<UserDto> Register(RegisterCommand command, CancellationToken ct = default) { /* ... */ }
    public virtual async Task<UserDto?> GetCurrentUser(Guid userId, CancellationToken ct = default) { /* ... */ }
}
```

### FluentValidation filter

`BuildingBlocks.Infrastructure/Commands/FluentValidationCommandHandler.cs`:

```csharp
public sealed class FluentValidationCommandHandler<TCommand>(IEnumerable<IValidator<TCommand>> validators)
    : ICommandHandler<TCommand>
    where TCommand : class, ICommand
{
    [CommandHandler(Priority = 1_000_000, IsFilter = true)]
    public async Task OnCommand(TCommand command, CommandContext context, CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var ctx = new ValidationContext<TCommand>(command);
            var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(ctx, cancellationToken))))
                .SelectMany(r => r.Errors).Where(f => f is not null).ToList();
            if (failures.Count > 0) throw new ValidationException(failures);
        }
        await context.InvokeRemainingHandlers(cancellationToken);
    }
}
```

`Priority = 1_000_000` filterni har qanday business handler'dan oldin yurguzadi (CommandR pipeline'da yuqori priority avval keladi). Eski MediatR `ValidationBehavior<,>` shu filter'ning to'liq o'rnini bosadi.

### Nima uchun MediatR emas

- Bitta paket (`ActualLab.Fusion`) — CQRS + reactive cache + invalidation + Blazor integration.
- Reactive `Computed<T>` — query natijalari avtomatik cache'lanadi, `Computed.Invalidate()` orqali yangilanadi (Fusion sub'larga signal yuboradi).
- Bitta service interface — modul'ning butun API'si, per-feature handler discovery yo'q.
- MIT/Apache litsenziyali (MediatR 12.5+ commercial bo'ldi).

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
