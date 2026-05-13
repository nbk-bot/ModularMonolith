# Running locally + adding modules

## Talablar

- .NET 10 SDK (`dotnet --list-sdks` da `10.0.x` bo'lishi kerak)
- Docker Desktop
- `dotnet-ef` global tool: `dotnet tool install --global dotnet-ef --version 10.0.0`

## Infra

```bash
docker compose up -d
```

Bu ko'taradi:
- `postgres:17` — `localhost:5432`, db `modmono`, user `postgres`, pass `postgres`
- `redis:7` — `localhost:6379`
- `rabbitmq:3-management` — AMQP `5672`, UI `http://localhost:15672` (guest/guest)
- `seq` — UI `http://localhost:5341` (log stream uchun)

## Migration'lar

Har bir modul o'z DbContext'i uchun alohida migration kerak. `Identity` va `Catalog` ikkalasi `Postgres` connectionga yo'naltirilgan lekin alohida schema'larda.

```bash
# Identity
dotnet ef migrations add Init \
  --project src/Modules/Identity/Identity.Infrastructure \
  --startup-project src/Api \
  --context IdentityDbContext

dotnet ef database update \
  --project src/Modules/Identity/Identity.Infrastructure \
  --startup-project src/Api \
  --context IdentityDbContext

# Catalog
dotnet ef migrations add Init \
  --project src/Modules/Catalog/Catalog.Infrastructure \
  --startup-project src/Api \
  --context CatalogDbContext

dotnet ef database update \
  --project src/Modules/Catalog/Catalog.Infrastructure \
  --startup-project src/Api \
  --context CatalogDbContext
```

Natija Postgres'da:

```
modmono
├── identity/        ← schema
│   ├── asp_net_users
│   ├── asp_net_roles
│   ├── asp_net_user_roles
│   ├── ... (boshqa Identity jadvallari)
│   └── __ef_migrations_history
└── catalog/         ← schema
    ├── products
    └── __ef_migrations_history
```

Hammasi snake_case (kichik harf, underscore).

## Ishga tushirish

```bash
dotnet run --project src/Api
```

Default `https://localhost:5001` portda ko'tariladi.

## Endpoint'lar

### REST

```
POST /api/auth/register         — { email, password, fullName? }
POST /api/auth/login            — { email, password } → { accessToken, refreshToken, expiresAt }
POST /api/auth/refresh          — { refreshToken } → { accessToken, refreshToken, expiresAt }
POST /api/auth/forgot-password  — { email }
POST /api/auth/reset-password   — { email, token, newPassword }
POST /api/auth/confirm-email    — { userId, token }
POST /api/auth/logout           — [Authorize] no body
GET  /api/products              — [AllowAnonymous] paginated list
POST /api/products              — [Authorize] { name, price, stock, description? }
GET  /api/products/export.xlsx  — [AllowAnonymous] Excel export
```

Postman/Bruno test:

```bash
# Register
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@local.dev","password":"Test123!"}'

# Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@local.dev","password":"Test123!"}'

# Product create (token kerak)
curl -X POST http://localhost:5000/api/products \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <accessToken>" \
  -d '{"name":"Test","price":10.50,"stock":5}'
```

### GraphQL

`POST /graphql`:

```graphql
query { me { id email roles } }
```

Banana Cake Pop IDE: brauzerda `https://localhost:5001/graphql` (Authorization headerni IDE Settings'da kiritasiz).

### gRPC

Catalog modul'da namuna service ulangan (`src/Modules/Catalog/Catalog.Presentation/Protos/catalog.proto`, `CatalogGrpcService`). Yangi `.proto` qo'shish:
1. `<Module>.Presentation`'da `Protos/Xxx.proto` yarating.
2. `.csproj`'ga:
   ```xml
   <Protobuf Include="Protos\Xxx.proto" GrpcServices="Server" />
   ```
3. Service implementatsiyasi yozing — `IXxxService` Fusion compute service'ni DI orqali oling va metodlarini chaqiring.
4. `Program.cs`'ga `app.MapGrpcService<XxxGrpcService>()` qo'shing.

## Yangi modul qo'shish

Misol: `Reporting` moduli.

### 1. Project'lar

```bash
cd ModularMonolith
dotnet new classlib -n Reporting.Domain         -o src/Modules/Reporting/Reporting.Domain         --framework net10.0
dotnet new classlib -n Reporting.Application    -o src/Modules/Reporting/Reporting.Application    --framework net10.0
dotnet new classlib -n Reporting.Infrastructure -o src/Modules/Reporting/Reporting.Infrastructure --framework net10.0
dotnet new classlib -n Reporting.Presentation   -o src/Modules/Reporting/Reporting.Presentation   --framework net10.0

dotnet sln add src/Modules/Reporting/Reporting.Domain/Reporting.Domain.csproj \
               src/Modules/Reporting/Reporting.Application/Reporting.Application.csproj \
               src/Modules/Reporting/Reporting.Infrastructure/Reporting.Infrastructure.csproj \
               src/Modules/Reporting/Reporting.Presentation/Reporting.Presentation.csproj
```

### 2. Reference'lar

```bash
dotnet add src/Modules/Reporting/Reporting.Domain reference src/BuildingBlocks/BuildingBlocks.Domain
dotnet add src/Modules/Reporting/Reporting.Application reference \
    src/Modules/Reporting/Reporting.Domain \
    src/BuildingBlocks/BuildingBlocks.Application
dotnet add src/Modules/Reporting/Reporting.Infrastructure reference \
    src/Modules/Reporting/Reporting.Application \
    src/BuildingBlocks/BuildingBlocks.Infrastructure
dotnet add src/Modules/Reporting/Reporting.Presentation reference \
    src/Modules/Reporting/Reporting.Application
```

### 3. `Reporting.Presentation.csproj`'ga FrameworkReference

```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

### 4. Compute service interface'i — `Reporting.Application/IReportingService.cs`

```csharp
using ActualLab.CommandR;
using ActualLab.CommandR.Configuration;
using ActualLab.Fusion;

namespace Reporting.Application;

public interface IReportingService : IComputeService
{
    [CommandHandler] Task<ReportDto> GenerateReport(GenerateReportCommand command, CancellationToken ct = default);
    [ComputeMethod]  Task<IReadOnlyList<ReportDto>> ListReports(CancellationToken ct = default);
}
```

Command record:

```csharp
public sealed record GenerateReportCommand(string Name) : ICommand<ReportDto>;
```

### 5. Implementation — `Reporting.Infrastructure/ReportingService.cs`

```csharp
public class ReportingService(ReportingDbContext db) : IReportingService
{
    public virtual async Task<ReportDto> GenerateReport(GenerateReportCommand command, CancellationToken ct = default)
    {
        // ...
    }

    public virtual async Task<IReadOnlyList<ReportDto>> ListReports(CancellationToken ct = default)
    {
        // ...
    }
}
```

Metodlar **`virtual`** — Fusion proxy intercept qila olishi uchun.

### 6. `Reporting.Infrastructure/DependencyInjection.cs`

```csharp
using ActualLab.Fusion;
using BuildingBlocks.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reporting.Application;

namespace Reporting.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Postgres")!;
        services.AddDbContext<ReportingDbContext>((sp, o) => o
            .UseNpgsql(conn, b => b.MigrationsHistoryTable("__ef_migrations_history", ReportingDbContext.DefaultSchema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<DomainEventDispatcherInterceptor>()));

        services.AddFusion().AddService<IReportingService, ReportingService>();
        services.AddValidatorsFromAssembly(typeof(IReportingService).Assembly);

        return services;
    }
}
```

### 7. `Api/Program.cs`

```csharp
builder.Services.AddBuildingBlocks(cfg);          // assembly parametri yo'q
builder.Services.AddIdentityModule(cfg);
builder.Services.AddCatalogModule(cfg);
builder.Services.AddReportingModule(cfg);          // ← qo'shildi
```

### 8. `Api/Api.csproj`

```xml
<ProjectReference Include="..\Modules\Reporting\Reporting.Infrastructure\Reporting.Infrastructure.csproj" />
<ProjectReference Include="..\Modules\Reporting\Reporting.Presentation\Reporting.Presentation.csproj" />
```

### 9. Controller — `Reporting.Presentation/ReportsController.cs`

```csharp
[ApiController]
[Route("api/reports")]
public sealed class ReportsController(IReportingService reporting) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReportDto>>> List(CancellationToken ct)
        => Ok(await reporting.ListReports(ct));

    [HttpPost]
    public async Task<ActionResult<ReportDto>> Generate([FromBody] GenerateReportRequest req, CancellationToken ct)
        => Ok(await reporting.GenerateReport(new GenerateReportCommand(req.Name), ct));
}
```

### 10. Migration

```bash
dotnet ef migrations add Init \
  --project src/Modules/Reporting/Reporting.Infrastructure \
  --startup-project src/Api \
  --context ReportingDbContext

dotnet ef database update \
  --project src/Modules/Reporting/Reporting.Infrastructure \
  --startup-project src/Api \
  --context ReportingDbContext
```

Yangi `reporting` schema Postgres'da paydo bo'ladi.

## Diagnostika

| Muammo | Yechim |
|---|---|
| `Postgres connection string missing` | `appsettings.json:ConnectionStrings:Postgres` to'g'rilangan emas |
| EF migrations'da `connection refused` | `docker compose up -d postgres` qilinmagan |
| `JWT signing key` xatosi | `Jwt:SigningKey` 32 belgi minimum bo'lishi kerak |
| `Fusion: method must be virtual` | Compute service implementation metodi `virtual` qilinmagan — `public virtual async Task<...>` ko'rinishida bo'lishi shart |
| `No handler found for command` | Modul'ning `AddXxxModule` ichida `services.AddFusion().AddService<IXxxService, XxxService>()` chaqirilmagan |
| `HotChocolate.Language` NU1904 warning | Hozircha ma'lum bug, ignoring xavfsiz (transitively only) |
