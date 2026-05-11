namespace BuildingBlocks.Domain;

public abstract class Entity<TId> : IEquatable<Entity<TId>> where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();

    public bool Equals(Entity<TId>? other) => other is not null && Id.Equals(other.Id);
    public override bool Equals(object? obj) => obj is Entity<TId> e && Equals(e);
    public override int GetHashCode() => Id.GetHashCode();
}

public interface IDomainEvent;

public abstract class AggregateRoot<TId> : Entity<TId> where TId : notnull;

public abstract class AuditableEntity<TId> : Entity<TId> where TId : notnull
{
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
}
