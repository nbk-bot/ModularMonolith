# Architecture

## Modular Monolith + Clean Architecture

Bir process, lekin har bir biznes-modul (Identity, Catalog, ...) o'z to'rt layer'iga ega:

```
Domain → Application → Infrastructure → Presentation
```

Modullararo aloqa:
- **Sinxron**: faqat public abstractions orqali (masalan, `AddIdentityModule` extension), hech qachon to'g'ridan-to'g'ri `DbContext`'ga emas.
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
- **Application** — Domain + `BuildingBlocks.Application` (MediatR/FluentValidation/Mapperly **abstractions**, contractlar).
- **Infrastructure** — Application + `BuildingBlocks.Infrastructure`. Mana shu yerda EF Core, Identity, MassTransit implementatsiyalari, command handler'lar.
- **Presentation** — Application (faqat command/query nomlari va DTO'lari). REST controllerlar, GraphQL resolvers, gRPC servicelari.
- **Api** — barcha module Infrastructure + Presentation'larni reference qiladi va `Program.cs`'da ularning DI extensionlarini chaqiradi.

Bu qoida har bir modul'da mustaqil saqlanadi. `Catalog.Domain` `Identity.Domain`ni ko'rmaydi.

## Nima uchun 4 ta projektga ajratish

| Layer | Sabab |
|---|---|
| Domain alohida | Test qilish oson, ASP.NET'siz domain logic, refactor xavfsiz |
| Application alohida | Contractlar (DTO, command, query) public, lekin handler'lar internal — outer world implementation detail'ni ko'rmaydi |
| Infrastructure alohida | EF Core/RabbitMQ versiyasini swap qilish Application'ga ta'sir qilmaydi |
| Presentation alohida | Bitta modul'ga bir nechta presentation ko'rinishi qo'shsa bo'ladi (REST + GraphQL + gRPC), Api faqat shu projektlarni referencelaydi |

## Domain Event vs Integration Event

| | Domain Event | Integration Event |
|---|---|---|
| Qachon | Aggregate ichida o'zgarish (`Product.UpdatePrice`) | Modul boshqa modulga xabar berishi kerak |
| Qaerda | Same module, same transaction | Cross-module yoki cross-service |
| Yo'l | In-memory (`MediatR.INotification`) | RabbitMQ (`MassTransit.IPublishEndpoint`) |
| Misol | `ProductPriceChanged` | `ProductCreatedIntegrationEvent` |
