# Modules

Har bir modul mustaqil 4 layer'li clean architecture, public API'si — `AddXxxModule(IConfiguration)` extension. `Api/Program.cs` shu extension'ni chaqiradi.

## Identity module

To'liq ASP.NET Core Identity stack: register, login (JWT + refresh), parol qoidalari, lockout, email/phone confirm tokenlari, role-based authorization.

### Identity.Domain

```csharp
public class ApplicationUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RefreshTokenExpiresAt { get; set; }
    public string? RefreshToken { get; set; }
}

public class ApplicationRole : IdentityRole<Guid>
{
    public string? Description { get; set; }
}
```

`Microsoft.Extensions.Identity.Stores` paketini ishlatadi — ASP.NET hosting'ga bog'liq emas, faqat Identity tip'larini olib keladi.

### Identity.Application

- **Contracts** (`AuthDtos.cs`): `RegisterRequest`, `LoginRequest`, `RefreshRequest`, `ForgotPasswordRequest`, `ResetPasswordRequest`, `ConfirmEmailRequest`, `AuthTokens`, `UserDto`.
- **Commands** (`Features/{Name}/{Name}Command.cs`): `RegisterCommand`, `LoginCommand` (MediatR `IRequest<T>`).
- **Validators**: `RegisterCommandValidator` (FluentValidation — email format + 8 belgi min parol).
- **Abstractions**: `ITokenService` interface.
- **Mappers**: `UserMapper` (Riok.Mapperly source-generated — `ApplicationUser → UserDto`).

### Identity.Infrastructure

- `IdentityDbContext : IdentityDbContext<ApplicationUser,ApplicationRole,Guid>` — `identity` schemasi.
- `TokenService` — JWT yaratish (HMAC-SHA256), refresh token cryptographically random 64 byte → base64.
- `RegisterCommandHandler` — `UserManager.CreateAsync`, xato bo'lsa `InvalidOperationException`.
- `LoginCommandHandler` — `SignInManager.CheckPasswordSignInAsync(lockoutOnFailure: true)`, success → access+refresh token, DB'ga refresh saqlash.
- `AddIdentityModule(IConfiguration)`:
  - DbContext + Npgsql + **`UseSnakeCaseNamingConvention()`** + per-schema migrations history (`identity.__ef_migrations_history`).
  - `AddIdentityCore<ApplicationUser>`:
    - `Password.RequireNonAlphanumeric = false`
    - `User.RequireUniqueEmail = true`
    - `SignIn.RequireConfirmedEmail = false` (production'da `true`)
    - `Lockout.DefaultLockoutTimeSpan = 5 daqiqa`, `MaxFailedAccessAttempts = 5`
  - `AddRoles<ApplicationRole>().AddEntityFrameworkStores<IdentityDbContext>().AddSignInManager().AddDefaultTokenProviders()` — `AddDefaultTokenProviders` email confirm/password reset tokenlarini ulаydi.
  - MediatR'ga shu assembly'dagi handlerlarni register.

### Identity.Presentation

- `AuthController`:
  - `POST /api/auth/register` → `RegisterCommand` → `UserDto`
  - `POST /api/auth/login` → `LoginCommand` → `AuthTokens` (access + refresh + expiresAt)
- `IdentityQueries` (GraphQL): `me` resolver, JWT claim'lardan ID/email/rollarni o'qiydi, `[Authorize]` bilan himoyalangan.

## Catalog module

Oddiy CRUD namuna, lekin to'liq domain modeling + RabbitMQ integration event publishing.

### Catalog.Domain

```csharp
public sealed class Product : AggregateRoot<Guid>
{
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public int Stock { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static Product Create(string name, decimal price, int stock, string? description = null)
    {
        // factory raises ProductCreated
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice <= 0) throw new ArgumentException(...);
        Price = newPrice;
        Raise(new ProductPriceChanged(Id, newPrice));
    }

    public void Restock(int amount) { ... }
}

public sealed record ProductCreated(Guid ProductId, string Name, decimal Price) : IDomainEvent;
public sealed record ProductPriceChanged(Guid ProductId, decimal NewPrice) : IDomainEvent;
```

E'tibor bering:
- `private set;` propertylar va static factory — invariantlar konstruktor orqali emas, factory metodida tekshiriladi.
- `UpdatePrice`/`Restock` — domain'da business qoida (`Price > 0`).
- Har bir o'zgarish `Raise(new ...)` qiladi → keyin Infrastructure layer'da MediatR notification'lari sifatida dispatch qilinishi mumkin.

### Catalog.Application

- `CreateProductCommand` + validator (Name not empty, Price > 0, Stock >= 0).
- `GetProductsQuery` — paginated (page + pageSize).
- `ProductCreatedIntegrationEvent : IntegrationEvent` — boshqa modullarga RabbitMQ orqali yuboriladi.
- `ProductMapper` (Mapperly) — `Product → ProductDto`, `IEnumerable<Product> → IReadOnlyList<ProductDto>`.

### Catalog.Infrastructure

- `CatalogDbContext : BaseDbContext` — `catalog` schemasi, `Products` DbSet, `ApplyConfigurationsFromAssembly`.
- `ProductConfiguration` — `numeric(18,2)` Price, max lengthlar, `b.Ignore(x => x.DomainEvents)` (collection persist qilinmasin).
- `CreateProductCommandHandler`:
  ```csharp
  var product = Product.Create(...);
  db.Products.Add(product);
  await db.SaveChangesAsync(ct);
  await bus.Publish(new ProductCreatedIntegrationEvent(...), ct);
  return product.ToDto();
  ```
- `GetProductsQueryHandler` — `AsNoTracking` + paging + Mapperly.
- `AddCatalogModule` — DbContext (snake_case) + MediatR.

### Catalog.Presentation

```csharp
[ApiController]
[Route("api/products")]
[Authorize]
public sealed class ProductsController(ISender sender) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    public async Task<...> List(int page = 1, int pageSize = 20, CancellationToken ct = default)
        => Ok(await sender.Send(new GetProductsQuery(page, pageSize), ct));

    [HttpPost]
    public async Task<...> Create(CreateProductRequest req, CancellationToken ct)
        => Ok(await sender.Send(new CreateProductCommand(...), ct));
}
```
