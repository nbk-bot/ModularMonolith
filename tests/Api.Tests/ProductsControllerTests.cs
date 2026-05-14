using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Catalog.Application.Contracts;
using FluentAssertions;
using Identity.Application.Contracts;
using Identity.Application.Features.Login;
using Identity.Application.Features.Register;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace Api.Tests;

/// <summary>
/// End-to-end HTTP tests against the real ASP.NET Core pipeline using
/// <see cref="WebApplicationFactory{Program}"/>. A <see cref="PostgreSqlContainer"/>
/// is started per-fixture; Redis and RabbitMQ are also expected to be available
/// locally for the registered health checks / MassTransit broker to wire up.
/// Marked with the <c>Integration</c> trait so CI can skip when Docker is not
/// available.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ProductsControllerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("modmono_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:3.13-management-alpine").Build();

    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync(), _rabbit.StartAsync());

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseEnvironment("Development");
                b.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
                b.UseSetting("ConnectionStrings:Redis", $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)}");
                b.UseSetting("ConnectionStrings:RabbitMQ", _rabbit.GetConnectionString());
                // LoggerBot is wired through GlobalErrorHandler; provide dummy values so DI
                // and error middleware don't crash on missing config in tests.
                b.UseSetting("LoggerBot:Token", "test-token");
                b.UseSetting("LoggerBot:ChatId", "0");
                // HS256 requires a 256-bit key — 32+ ASCII chars satisfies that.
                b.UseSetting("Jwt:SigningKey", "test-signing-key-must-be-at-least-32-bytes-long-for-hs256");
            });
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask(), _rabbit.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task Get_Products_Returns_200_AndEmptyList()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<ProductDto>>();
        items.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task Post_Products_Without_Auth_Returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/products",
            new CreateProductRequest("X", 10m, 1, null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_Login_CreateProduct_Succeeds()
    {
        var client = _factory.CreateClient();
        var email = $"u{Guid.NewGuid():N}@test.local";

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "Password1!", "Test User"));
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "Password1!"));
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadFromJsonAsync<AuthTokens>();
        tokens!.AccessToken.Should().NotBeNullOrEmpty();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var create = await client.PostAsJsonAsync("/api/products",
            new CreateProductRequest("Phone", 199.99m, 3, "desc"));
        create.EnsureSuccessStatusCode();

        var product = await create.Content.ReadFromJsonAsync<ProductDto>();
        product!.Name.Should().Be("Phone");
        product.Price.Should().Be(199.99m);
    }
}
