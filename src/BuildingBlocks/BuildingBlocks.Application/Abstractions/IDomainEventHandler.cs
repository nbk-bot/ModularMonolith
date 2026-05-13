using BuildingBlocks.Domain;

namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Marker for handlers reacting to a specific <see cref="IDomainEvent"/>.
/// Resolved by <c>DomainEventDispatcherInterceptor</c> via
/// <see cref="System.IServiceProvider"/> after the EF Core transaction commits
/// — no MediatR involvement.
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}
