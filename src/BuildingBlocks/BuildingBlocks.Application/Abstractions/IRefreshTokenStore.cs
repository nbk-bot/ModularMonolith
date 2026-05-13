namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Abstraction over refresh-token persistence so that cross-cutting jobs
/// (e.g. Coravel cleanup) in BuildingBlocks can clear expired tokens without
/// taking a project reference on the Identity module.
/// </summary>
public interface IRefreshTokenStore
{
    Task<int> RemoveExpiredAsync(CancellationToken ct = default);
}
