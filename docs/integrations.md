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
