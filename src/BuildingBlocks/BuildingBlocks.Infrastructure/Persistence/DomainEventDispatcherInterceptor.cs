using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Collects <see cref="IDomainEvent"/>s from tracked <c>Entity&lt;T&gt;</c>
/// aggregates and dispatches them via <see cref="IDomainEventHandler{TEvent}"/>
/// implementations resolved from <see cref="IServiceProvider"/> <i>after</i>
/// the transaction commits successfully. Registered as a singleton interceptor
/// on every <see cref="DbContext"/> in the solution so both
/// <c>CatalogDbContext</c> (which derives from <see cref="BaseDbContext"/>) and
/// <c>IdentityDbContext</c> (which must derive from ASP.NET Core's
/// <c>IdentityDbContext&lt;...&gt;</c> and cannot inherit from
/// <see cref="BaseDbContext"/>) get the same behaviour.
/// Replaces the old MediatR-based fan-out — handlers are resolved by
/// reflection (the simpler path noted in the refactor brief).
/// </summary>
public sealed class DomainEventDispatcherInterceptor(IServiceProvider services) : SaveChangesInterceptor
{
    // Per-context pending events; keyed by ContextId so concurrent saves don't collide.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, List<IDomainEvent>> _pending = new();

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var ctx = eventData.Context;
        if (ctx is not null)
        {
            var events = ExtractAndClear(ctx);
            if (events.Count > 0)
                _pending[ctx.ContextId.InstanceId] = events;
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        var ctx = eventData.Context;
        if (ctx is not null && _pending.TryRemove(ctx.ContextId.InstanceId, out var events))
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;
            foreach (var evt in events)
                await DispatchAsync(sp, evt, cancellationToken);
        }
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is not null)
            _pending.TryRemove(eventData.Context.ContextId.InstanceId, out _);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            _pending.TryRemove(eventData.Context.ContextId.InstanceId, out _);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private static async Task DispatchAsync(IServiceProvider sp, IDomainEvent evt, CancellationToken ct)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(evt.GetType());
        var handlers = (System.Collections.IEnumerable)sp.GetServices(handlerType);
        var method = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync));
        if (method is null) return;

        foreach (var handler in handlers)
        {
            if (handler is null) continue;
            var task = (Task?)method.Invoke(handler, [evt, ct]);
            if (task is not null) await task;
        }
    }

    private static List<IDomainEvent> ExtractAndClear(DbContext ctx)
    {
        var events = new List<IDomainEvent>();
        foreach (var entry in ctx.ChangeTracker.Entries())
        {
            if (entry.Entity is not { } entity) continue;
            // Entity<T> is generic; the DomainEvents/ClearDomainEvents members are non-generic by name.
            var type = entity.GetType();
            var dePropName = nameof(Entity<int>.DomainEvents);
            var clearName = nameof(Entity<int>.ClearDomainEvents);
            var deProp = type.GetProperty(dePropName);
            if (deProp is null) continue;

            if (deProp.GetValue(entity) is IEnumerable<IDomainEvent> list)
            {
                var snapshot = list.ToList();
                if (snapshot.Count == 0) continue;
                events.AddRange(snapshot);
                type.GetMethod(clearName)?.Invoke(entity, null);
            }
        }
        return events;
    }
}
