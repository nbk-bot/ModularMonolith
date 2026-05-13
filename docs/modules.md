# Modules

Har bir modul mustaqil 4 layer'li clean architecture, public API'si — `AddXxxModule(IConfiguration)` extension va `IXxxService : IComputeService` Fusion compute service interface'i. `Api/Program.cs` shu extension'ni chaqiradi; controllerlar va GraphQL resolverlar `IXxxService`'ni to'g'ridan-to'g'ri DI orqali oladi.

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
- **Commands** (`Features/{Name}/{Name}Command.cs`): `RegisterCommand`, `LoginCommand`, `RefreshTokenCommand`, `ForgotPasswordCommand`, `ResetPasswordCommand`, `ConfirmEmailCommand`, `LogoutCommand` — har biri `ActualLab.CommandR.ICommand<TResult>` (yoki `ICommand` agar javob yo'q bo'lsa) record'i.
- **Validators**: `RegisterCommandValidator` (FluentValidation — email format + 8 belgi min parol), va boshqa command'lar uchun mos validatorlar.
- **Abstractions**: `ITokenService` interface.
- **Mappers**: `UserMapper` (Riok.Mapperly source-generated — `ApplicationUser → UserDto`).
- **`IIdentityService : IComputeService`** — modul'ning yagona public Fusion compute service interface'i:

```csharp
public interface IIdentityService : IComputeService
{
    [CommandHandler] Task<UserDto>    Register(RegisterCommand command, CancellationToken ct = default);
    [CommandHandler] Task<AuthTokens> Login(LoginCommand command, CancellationToken ct = default);
    [CommandHandler] Task<AuthTokens> Refresh(RefreshTokenCommand command, CancellationToken ct = default);
    [CommandHandler] Task ForgotPassword(ForgotPasswordCommand command, CancellationToken ct = default);
    [CommandHandler] Task ResetPassword(ResetPasswordCommand command, CancellationToken ct = default);
    [CommandHandler] Task ConfirmEmail(ConfirmEmailCommand command, CancellationToken ct = default);
    [CommandHandler] Task Logout(LogoutCommand command, CancellationToken ct = default);
    [ComputeMethod]  Task<UserDto?> GetCurrentUser(Guid userId, CancellationToken ct = default);
}
```

### Identity.Infrastructure

- `IdentityDbContext : IdentityDbContext<ApplicationUser,ApplicationRole,Guid>` — `identity` schemasi.
- `TokenService` — JWT yaratish (HMAC-SHA256), refresh token cryptographically random 64 byte → base64.
- `IdentityService : IIdentityService` — modul'ning yagona Fusion compute service implementatsiyasi. Eski per-feature `RegisterCommandHandler`, `LoginCommandHandler`, `RefreshCommandHandler` va boshqalar shu class'ning `virtual` metodlariga jamlandi:
  - `Register` → `UserManager.CreateAsync`, xato bo'lsa `InvalidOperationException`.
  - `Login` → `SignInManager.CheckPasswordSignInAsync(lockoutOnFailure: true)`, success → access+refresh token, DB'ga refresh saqlash.
  - `Refresh` → DB'da refresh tokenni topish + expiry tekshirish → yangi access+refresh.
  - `ForgotPassword`/`ResetPassword`/`ConfirmEmail` — `UserManager`'ning mos token providerlari orqali.
  - `Logout` → DB'dagi refresh tokenni tozalash.
- `AddIdentityModule(IConfiguration)`:
  - DbContext + Npgsql + **`UseSnakeCaseNamingConvention()`** + per-schema migrations history (`identity.__ef_migrations_history`) + `DomainEventDispatcherInterceptor`.
  - `AddIdentityCore<ApplicationUser>`:
    - `Password.RequireNonAlphanumeric = false`
    - `User.RequireUniqueEmail = true`
    - `SignIn.RequireConfirmedEmail = false` (production'da `true`)
    - `Lockout.DefaultLockoutTimeSpan = 5 daqiqa`, `MaxFailedAccessAttempts = 5`
  - `AddRoles<ApplicationRole>().AddEntityFrameworkStores<IdentityDbContext>().AddSignInManager().AddDefaultTokenProviders()` — `AddDefaultTokenProviders` email confirm/password reset tokenlarini ulаydi.
  - `services.AddScoped<ITokenService, TokenService>();`
  - `services.AddFusion().AddService<IIdentityService, IdentityService>();` — Fusion proxy generation va `ICommander` registration shu yerda.
  - `services.AddValidatorsFromAssembly(typeof(IIdentityService).Assembly);` — FluentValidation moduldagi barcha validatorlarni topadi.

### Identity.Presentation

- `AuthController` (constructor injection: `IIdentityService identity`):
  - `POST /api/auth/register` → `identity.Register(new RegisterCommand(...))` → `UserDto`
  - `POST /api/auth/login` → `identity.Login(...)` → `AuthTokens`
  - `POST /api/auth/refresh` → `identity.Refresh(...)`
  - `POST /api/auth/forgot-password` / `reset-password` / `confirm-email` — mos commandlar.
  - `[Authorize] POST /api/auth/logout` — `sub` claimdan userId olib `identity.Logout(...)`.
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
- Har bir o'zgarish `Raise(new ...)` qiladi → keyin `DomainEventDispatcherInterceptor` `SavedChangesAsync` paytida tracked entitylardan eventlarni yig'adi va DI'dagi har bir `IDomainEventHandler<T>`'ga yetkazadi.

### Catalog.Application

- `CreateProductCommand : ICommand<ProductDto>` + validator (Name not empty, Price > 0, Stock >= 0).
- `ProductCreatedIntegrationEvent : IntegrationEvent` — boshqa modullarga RabbitMQ orqali yuboriladi.
- `ProductMapper` (Mapperly) — `Product → ProductDto`, `IEnumerable<Product> → IReadOnlyList<ProductDto>`.
- **`ICatalogService : IComputeService`**:

```csharp
public interface ICatalogService : IComputeService
{
    [CommandHandler] Task<ProductDto> CreateProduct(CreateProductCommand command, CancellationToken ct = default);
    [ComputeMethod]  Task<IReadOnlyList<ProductDto>> GetProducts(int page = 1, int pageSize = 20, CancellationToken ct = default);
}
```

`GetProductsQuery` recordi yo'q — `GetProducts` Fusion `[ComputeMethod]` sifatida to'g'ridan-to'g'ri argumentlar oladi va Computed natijasini cache'laydi.

### Catalog.Infrastructure

- `CatalogDbContext : BaseDbContext` — `catalog` schemasi, `Products` DbSet, `ApplyConfigurationsFromAssembly`.
- `ProductConfiguration` — `numeric(18,2)` Price, max lengthlar, `b.Ignore(x => x.DomainEvents)` (collection persist qilinmasin).
- `CatalogService : ICatalogService` — modul'ning yagona Fusion compute service. Eski `CreateProductCommandHandler` + `GetProductsQueryHandler` shu class'ning `virtual` metodlariga jamlangan:

```csharp
public class CatalogService(CatalogDbContext db, IPublishEndpoint bus) : ICatalogService
{
    public virtual async Task<ProductDto> CreateProduct(CreateProductCommand command, CancellationToken ct = default)
    {
        var product = Product.Create(command.Name, command.Price, command.Stock, command.Description);
        db.Products.Add(product);
        await bus.Publish(new ProductCreatedIntegrationEvent(product.Id, product.Name, product.Price), ct);
        await db.SaveChangesAsync(ct);
        return product.ToDto();
    }

    public virtual async Task<IReadOnlyList<ProductDto>> GetProducts(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        var items = await db.Products.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return items.ToDtoList();
    }
}
```

Metodlar **`virtual`** — Fusion proxy ularga `[CommandHandler]` / `[ComputeMethod]` semantikasini intercept qilishi uchun.

- `AddCatalogModule` — DbContext (snake_case) + `services.AddFusion().AddService<ICatalogService, CatalogService>()` + `services.AddValidatorsFromAssembly(typeof(ICatalogService).Assembly)`.

### Catalog.Presentation

```csharp
[ApiController]
[Route("api/products")]
[Authorize]
public sealed class ProductsController(ICatalogService catalog) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> List(int page = 1, int pageSize = 20, CancellationToken ct = default)
        => Ok(await catalog.GetProducts(page, pageSize, ct));

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductRequest req, CancellationToken ct)
        => Ok(await catalog.CreateProduct(new CreateProductCommand(req.Name, req.Price, req.Stock, req.Description), ct));
}
```

`ISender`/`IMediator` o'rniga to'g'ridan-to'g'ri `ICatalogService`. Fusion proxy `CreateProduct`'ni `ICommander`'ga, `GetProducts`'ni Computed cache'iga yo'naltiradi.
