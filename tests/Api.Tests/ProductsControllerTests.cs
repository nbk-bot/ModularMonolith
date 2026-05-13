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
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("modmono_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseEnvironment("Development");
                b.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            });
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await _postgres.DisposeAsync();
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
