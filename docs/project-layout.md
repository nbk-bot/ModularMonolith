# Project Layout

```
src/
├── Api/                                  ← Composition root (Program.cs)
├── BuildingBlocks/
│   ├── BuildingBlocks.Domain/            ← Entity<T>, AggregateRoot<T>, IDomainEvent
│   ├── BuildingBlocks.Application/       ← ICacheService, IUnitOfWork, IntegrationEvent, IDomainEventHandler<>, ISmsSender
│   └── BuildingBlocks.Infrastructure/    ← AddBuildingBlocks/AddMessaging DI, Fusion + CommandR wiring, FluentValidationCommandHandler<>, Redis, JWT, BaseDbContext, DomainEventDispatcherInterceptor
├── Modules/
│   ├── Identity/
│   │   ├── Identity.Domain/
│   │   ├── Identity.Application/         ← IIdentityService : IComputeService + Features/*/XxxCommand records
│   │   ├── Identity.Infrastructure/      ← IdentityDbContext, TokenService, IdentityService (Fusion impl)
│   │   └── Identity.Presentation/        ← AuthController (injects IIdentityService directly)
│   └── Catalog/
│       ├── Catalog.Domain/
│       ├── Catalog.Application/          ← ICatalogService : IComputeService + CreateProductCommand
│       ├── Catalog.Infrastructure/       ← CatalogDbContext, CatalogService (Fusion impl)
│       └── Catalog.Presentation/         ← ProductsController, ProductsExcelExporter, gRPC service
└── tests/
```

## Project references

| Project | References |
|---|---|
| `BuildingBlocks.Domain` | — |
| `BuildingBlocks.Application` | `BuildingBlocks.Domain` |
| `BuildingBlocks.Infrastructure` | `BuildingBlocks.Application` |
| `{Module}.Domain` | `BuildingBlocks.Domain` |
| `{Module}.Application` | `{Module}.Domain` + `BuildingBlocks.Application` |
| `{Module}.Infrastructure` | `{Module}.Application` + `BuildingBlocks.Infrastructure` |
| `{Module}.Presentation` | `{Module}.Application` |
| `Api` | `BuildingBlocks.Infrastructure` + all `{Module}.Infrastructure` + all `{Module}.Presentation` |

Diqqat: `Presentation` `Infrastructure`'ni reference qilmaydi — implementation Infrastructure'da `XxxService : IXxxService` shaklida joylashgan, Presentation faqat `IXxxService` (Fusion compute service) interface'ini DI orqali oladi va uning metodlarini to'g'ridan-to'g'ri chaqiradi (`ISender`/`IMediator` o'rniga).

## Har bir BuildingBlock'da nima bor

### `BuildingBlocks.Domain`

| File | Mazmun |
|---|---|
| `Entity.cs` | `Entity<TId>`, `AggregateRoot<TId>`, `AuditableEntity<TId>`, `IDomainEvent` |

### `BuildingBlocks.Application`

| Folder | Content |
|---|---|
| `Abstractions/` | `ICacheService`, `IUnitOfWork`, `IIntegrationEvent`, `IntegrationEvent` record, `IDomainEventHandler<TEvent>`, `ISmsSender` |

`ValidationBehavior` endi yo'q — Fusion CommandR uchun open-generic filter `BuildingBlocks.Infrastructure`'ga ko'chirilgan.

### `BuildingBlocks.Infrastructure`

| Folder/File | Content |
|---|---|
| `Commands/FluentValidationCommandHandler.cs` | Open-generic `ICommandHandler<TCommand>` — `[CommandHandler(Priority = 1_000_000, IsFilter = true)]`. Har bir Fusion command'dan oldin yurib FluentValidation tekshiruvini bajaradi. Eski MediatR `ValidationBehavior<,>`'ning o'rnini bosadi. |
| `Persistence/BaseDbContext.cs` | Module DbContext'lari uchun abstract base, `Schema` property orqali Postgres schemasi |
| `Persistence/DomainEventDispatcherInterceptor.cs` | `SaveChangesInterceptor` — transaction muvaffaqiyatli bo'lgandan keyin tracked entitylardan `IDomainEvent`'larni yig'adi va DI'dagi har bir `IDomainEventHandler<T>` ga yetkazadi |
| `Caching/RedisCacheService.cs` | `ICacheService` Redis implementatsiyasi |
| `Authentication/JwtOptions.cs` | `appsettings.json:Jwt` bo'limi binding |
| `DependencyInjection.cs` | `AddBuildingBlocks(IConfiguration)` (Fusion + CommandR + FluentValidation filter + JWT + Redis + health + CORS + Eskiz + LoggerBot) va `AddMessaging(IConfiguration, extra)` |

`AddBuildingBlocks` endi `applicationAssemblies` parametr olmaydi — handlerlarni assembly'dan topish kerak emas, har modul'ning `AddXxxModule` ichida `services.AddFusion().AddService<IXxxService, XxxService>()` va `services.AddValidatorsFromAssembly(typeof(IXxxService).Assembly)` chaqiriladi.
