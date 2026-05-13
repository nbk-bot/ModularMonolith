# Architecture

## Modular Monolith + Clean Architecture

Bir process, lekin har bir biznes-modul (Identity, Catalog, ...) o'z to'rt layer'iga ega:

```
Domain → Application → Infrastructure → Presentation
```

Modullararo aloqa:
- **Sinxron**: faqat public abstractions orqali (masalan, `AddIdentityModule` extension va `IIdentityService` Fusion compute service interface'i), hech qachon to'g'ridan-to'g'ri `DbContext`'ga emas.
- **Asinxron**: RabbitMQ integration eventlari (`IntegrationEvent` recordi orqali, MassTransit `IPublishEndpoint`).

Buning afzalligi:
- Bitta process — deployment, debugging, transaction'lar oson.
- Modullar haqiqatda izolyatsiyalangan — kelajakda biror modulni alohida servisga ajratish kerak bo'lsa, asynхron contract allaqachon RabbitMQ orqali.

## Clean Architecture dependency rule

```
Presentation ──┐
                ├──► Application ──► Domain
Infrastructure ┘
```

- **Domain** — hech qaysi infrastructure'ga bog'liq emas. Faqat `BuildingBlocks.Domain` (Entity base) va Identity uchun `Microsoft.Extensions.Identity.Stores` (ASP.NET'siz pokiza Identity tip'lari).
- **Application** — Domain + `BuildingBlocks.Application` (`ActualLab.Fusion` / `ActualLab.CommandR` abstractions, `FluentValidation`, `Riok.Mapperly` abstractions, contractlar va `IXxxService : IComputeService` interface'lari).
- **Infrastructure** — Application + `BuildingBlocks.Infrastructure`. Mana shu yerda EF Core, Identity, MassTransit implementatsiyalari, va Fusion compute service implementatsiyasi (`IdentityService`, `CatalogService`).
- **Presentation** — Application (faqat command/query rekordlari, DTO'lar va `IXxxService` interfeysi). REST controllerlar, GraphQL resolvers, gRPC servicelari to'g'ridan-to'g'ri compute service'ni DI orqali oladi.
- **Api** — barcha module Infrastructure + Presentation'larni reference qiladi va `Program.cs`'da ularning DI extensionlarini chaqiradi.

Bu qoida har bir modul'da mustaqil saqlanadi. `Catalog.Domain` `Identity.Domain`ni ko'rmaydi.

## Nima uchun 4 ta projektga ajratish

| Layer | Sabab |
|---|---|
| Domain alohida | Test qilish oson, ASP.NET'siz domain logic, refactor xavfsiz |
| Application alohida | Contractlar (DTO, command, `IXxxService` interface) public — Presentation faqat shu interface orqali ishlaydi, implementation detail'ni ko'rmaydi |
| Infrastructure alohida | EF Core/RabbitMQ versiyasini swap qilish Application'ga ta'sir qilmaydi |
| Presentation alohida | Bitta modul'ga bir nechta presentation ko'rinishi qo'shsa bo'ladi (REST + GraphQL + gRPC), Api faqat shu projektlarni referencelaydi |

## CQRS — ActualLab.Fusion / CommandR

MediatR o'rniga **ActualLab.Fusion** va uning ichidagi **CommandR** ishlatamiz. Har modul bitta public Fusion compute service interface'ini chiqaradi (`IIdentityService`, `ICatalogService`) va u `IComputeService`'dan inherit qiladi:

```csharp
public interface ICatalogService : IComputeService
{
    [CommandHandler] Task<ProductDto> CreateProduct(CreateProductCommand command, CancellationToken ct = default);
    [ComputeMethod]  Task<IReadOnlyList<ProductDto>> GetProducts(int page = 1, int pageSize = 20, CancellationToken ct = default);
}
```

- `[CommandHandler]` metodlar `ICommand<TResult>` recordlarni qabul qiladi va Fusion `ICommander` orqali yo'naltiriladi.
- `[ComputeMethod]` metodlar reactive `Computed<T>` natija beradi — Fusion ularni cache'laydi va invalidation orqali yangilaydi.
- FluentValidation `AddBuildingBlocks` ichida open-generic `FluentValidationCommandHandler<>` filter sifatida registratsiya qilingan (`Priority = 1_000_000`, `IsFilter = true`) — har bir command business handler'dan oldin yuradi.
- Eski per-feature `*CommandHandler`/`*QueryHandler` class'lari yo'q — implementation har modul'ning `Infrastructure` layer'ida bitta `XxxService : IXxxService` class'iga jamlangan (metodlar `virtual` — Fusion intercept qila olishi uchun).

## Domain Event vs Integration Event

| | Domain Event | Integration Event |
|---|---|---|
| Qachon | Aggregate ichida o'zgarish (`Product.UpdatePrice`) | Modul boshqa modulga xabar berishi kerak |
| Qaerda | Same module, same transaction | Cross-module yoki cross-service |
| Yo'l | In-memory (`IDomainEvent` → `DomainEventDispatcherInterceptor` → `IDomainEventHandler<T>`) | RabbitMQ (`MassTransit.IPublishEndpoint`, EF outbox bilan) |
| Misol | `ProductPriceChanged` | `ProductCreatedIntegrationEvent` |

Domain eventlar `SaveChangesInterceptor` orqali transaction muvaffaqiyatli bo'lgandan keyin DI'dan barcha `IDomainEventHandler<T>` implementatsiyalariga yetkaziladi — MediatR `INotification` o'rniga.
