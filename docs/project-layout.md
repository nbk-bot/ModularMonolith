# Project Layout

```
src/
├── Api/                                  ← Composition root (Program.cs)
├── BuildingBlocks/
│   ├── BuildingBlocks.Domain/            ← Entity<T>, AggregateRoot<T>, IDomainEvent
│   ├── BuildingBlocks.Application/       ← ICacheService, IUnitOfWork, IntegrationEvent, ValidationBehavior
│   └── BuildingBlocks.Infrastructure/    ← AddBuildingBlocks/AddMessaging DI, Redis, JWT, BaseDbContext
├── Modules/
│   ├── Identity/
│   │   ├── Identity.Domain/
│   │   ├── Identity.Application/
│   │   ├── Identity.Infrastructure/
│   │   └── Identity.Presentation/
│   └── Catalog/
│       ├── Catalog.Domain/
│       ├── Catalog.Application/
│       ├── Catalog.Infrastructure/
│       └── Catalog.Presentation/
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

Diqqat: `Presentation` `Infrastructure`'ni reference qilmaydi — handler'lar Infrastructure'da, Presentation faqat `ISender` (MediatR) orqali ularni chaqiradi.

## Har bir BuildingBlock'da nima bor

### `BuildingBlocks.Domain`

| File | Mazmun |
|---|---|
| `Entity.cs` | `Entity<TId>`, `AggregateRoot<TId>`, `AuditableEntity<TId>`, `IDomainEvent` |

### `BuildingBlocks.Application`

| Folder | Content |
|---|---|
| `Abstractions/` | `ICacheService`, `IUnitOfWork`, `IIntegrationEvent`, `IntegrationEvent` record |
| `Behaviors/` | `ValidationBehavior<TRequest,TResponse>` — MediatR pipeline behavior, command oldidan FluentValidation'ni yuradi |

### `BuildingBlocks.Infrastructure`

| Folder/File | Content |
|---|---|
| `Persistence/BaseDbContext.cs` | Module DbContext'lari uchun abstract base, `Schema` property orqali Postgres schemasi |
| `Caching/RedisCacheService.cs` | `ICacheService` Redis implementatsiyasi |
| `Authentication/JwtOptions.cs` | `appsettings.json:Jwt` bo'limi binding |
| `DependencyInjection.cs` | `AddBuildingBlocks(IConfiguration, applicationAssemblies)` + `AddMessaging(IConfiguration, extra)` |
