using System.Text.Json;
using BuildingBlocks.Application.Abstractions;
using StackExchange.Redis;

namespace BuildingBlocks.Infrastructure.Caching;

public sealed class RedisCacheService(IConnectionMultiplexer redis) : ICacheService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync(key);
        return value.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(value.ToString());
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
        => _db.StringSetAsync(key, JsonSerializer.Serialize(value), ttl);

    public Task RemoveAsync(string key, CancellationToken ct = default)
        => _db.KeyDeleteAsync(key);
}
