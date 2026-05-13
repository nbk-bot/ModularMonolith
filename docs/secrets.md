# Secret management

Hech qachon real maxfiy qiymatlarni `appsettings.json`'ga commit qilmang. `SigningKey` va boshqa secretlar uchun quyidagi workflow ishlatiladi.

## Development — User Secrets

`src/Api/Api.csproj`'da `<UserSecretsId>modmono-api-dev</UserSecretsId>` propertyi mavjud. Lokal mashinada:

```bash
cd src/Api
dotnet user-secrets init   # bir martalik (UserSecretsId allaqachon o'rnatilgan)
dotnet user-secrets set "Jwt:SigningKey" "<32-belgidan-kam-bo'lmagan-strong-secret>"
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=...;Username=...;Password=..."
```

User secrets `%APPDATA%\Microsoft\UserSecrets\modmono-api-dev\secrets.json` (Windows) / `~/.microsoft/usersecrets/modmono-api-dev/secrets.json` (Linux/macOS)'da saqlanadi — repodan tashqarida.

## Staging / Production — Environment variables

`__` (ikkita pastki chiziq) bo'limni belgilaydi:

```bash
export Jwt__SigningKey="<strong-secret-min-32-chars>"
export ConnectionStrings__Postgres="Host=...;Username=...;Password=..."
export Cors__AllowedOrigins__0="https://app.example.com"
```

Docker Compose / Kubernetes secret yoki cloud secret manager orqali inject qiling.

## Azure Key Vault (optional)

Paket: `Azure.Extensions.AspNetCore.Configuration.Secrets`. `Program.cs`'da:

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri("https://<vault-name>.vault.azure.net/"),
    new DefaultAzureCredential());
```

`Jwt--SigningKey` nomi bilan secret yaratilgan bo'lsa, u `Jwt:SigningKey` konfiguratsiyaga avtomatik bog'lanadi.

## Qoidalar

- `appsettings.json` faqat **placeholder** qiymatlar (`REPLACE_ME_32_CHARS_MIN`).
- `appsettings.Development.json` git'ga commit qilinadi — lekin shu yerga ham real secretlar yozilmaydi (user secrets afzal).
- `appsettings.Production.json` — non-secret config (CORS origins, logging override). Real secretlar env / Key Vault'dan.
- CI/CD'da yangi env yaratilganda Jwt SigningKey ni rotate qilish skripti bo'lsin.
