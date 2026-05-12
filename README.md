# ModularMonolith

Clean Architecture modular monolith template built on **.NET 10**.

## Stack

- ASP.NET Core 10 (Web API + Controllers)
- EF Core 10 + Npgsql + **EFCore.NamingConventions** (snake_case by default)
- Dapper (raw SQL when EF is overkill)
- PostgreSQL 17
- Riok.Mapperly (source-generated mapping)
- MediatR + FluentValidation (CQRS + validation pipeline)
- MassTransit + RabbitMQ (integration events)
- HotChocolate (GraphQL)
- gRPC (`Grpc.AspNetCore`)
- StackExchange.Redis (distributed cache via `ICacheService`)
- Coravel (scheduling/queues)
- Serilog (console + rolling file + Seq sink)
- DocumentFormat.OpenXml (Excel exports)
- ASP.NET Core Identity (`IdentityCore<ApplicationUser>` + roles + JWT + lockout + token providers)

## Layout

```
src/
├── Api/                              # Composition root (Program.cs)
├── BuildingBlocks/
│   ├── BuildingBlocks.Domain/        # Entity<T>, AggregateRoot<T>, IDomainEvent
│   ├── BuildingBlocks.Application/   # ICacheService, IIntegrationEvent, ValidationBehavior
│   └── BuildingBlocks.Infrastructure/# DI extensions, RedisCacheService, JWT, BaseDbContext, AddMessaging
├── Modules/
│   ├── Identity/                     # AspNet Identity + JWT + GraphQL Me query
│   │   ├── Identity.Domain/
│   │   ├── Identity.Application/
│   │   ├── Identity.Infrastructure/  # IdentityDbContext, TokenService, handlers
│   │   └── Identity.Presentation/    # AuthController, IdentityQueries
│   └── Catalog/                      # CRUD example with MassTransit publishing
│       ├── Catalog.Domain/           # Product aggregate + domain events
│       ├── Catalog.Application/
│       ├── Catalog.Infrastructure/   # CatalogDbContext + handlers
│       └── Catalog.Presentation/     # ProductsController
└── tests/
```

Per module: each `AddXxxModule` extension boots its own EF Core `DbContext` in its own Postgres schema (`identity`, `catalog`) and registers MediatR handlers from the module's Infrastructure assembly.

## Snake_case naming

All EF Core `DbContext`s opt into snake_case automatically via `EFCore.NamingConventions`:

```csharp
services.AddDbContext<CatalogDbContext>(o => o
    .UseNpgsql(conn)
    .UseSnakeCaseNamingConvention());
```

`Products.Name` → `catalog.products.name`, etc.

## Run locally

```bash
# Bring up Postgres + Redis + RabbitMQ + Seq
docker compose up -d

# First-time migrations (per module)
dotnet ef migrations add Init --project src/Modules/Identity/Identity.Infrastructure --startup-project src/Api -- --context IdentityDbContext
dotnet ef database update --project src/Modules/Identity/Identity.Infrastructure --startup-project src/Api --context IdentityDbContext

dotnet ef migrations add Init --project src/Modules/Catalog/Catalog.Infrastructure --startup-project src/Api -- --context CatalogDbContext
dotnet ef database update --project src/Modules/Catalog/Catalog.Infrastructure --startup-project src/Api --context CatalogDbContext

# Run
dotnet run --project src/Api
```

## Endpoints

- `POST /api/auth/register` — create account
- `POST /api/auth/login` — issue JWT + refresh token
- `GET /api/products` (anonymous), `POST /api/products` (auth)
- `GET /api/products/export.xlsx` — OpenXML export of all products
- `POST /graphql` — `query { me { id email roles } }` (auth)
- gRPC: `CatalogGrpc.ListProducts` (`src/Modules/Catalog/Catalog.Presentation/Protos/catalog.proto`)
- `GET /healthz` — Postgres + Redis + RabbitMQ health probe
- `GET /openapi/v1.json` (Development only) — OpenAPI doc with `bearerAuth` JWT scheme

## Adding a new module

1. `dotnet new classlib -n Foo.Domain -o src/Modules/Foo/Foo.Domain --framework net10.0` (×4 — same for Application/Infrastructure/Presentation)
2. Reference building blocks the same way Catalog does.
3. Add `AddFooModule` to `src/Modules/Foo/Foo.Infrastructure/DependencyInjection.cs`.
4. Call it from `Api/Program.cs`.

## Known issues

- `HotChocolate.Language` 14.3.0 / 15.0.0 has GHSA-qr3m-xw4c-jqw3 — bump when a fixed version is published.
- `AspNetCore.HealthChecks.Rabbitmq` 9.0.0 no longer accepts a connection string overload (RabbitMQ.Client v7 dropped that API); we register a singleton `IConnection` and the health check resolves it from DI. Eager connection at startup means the host throws if RabbitMQ is unreachable — fine for `docker compose` development, switch to lazy connection for production.
- Dev seed (`IdentitySeeder`) provisions `admin@local.dev` / `Admin123!` only when `IWebHostEnvironment.IsDevelopment()` is true.
