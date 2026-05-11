namespace BuildingBlocks.Application.Abstractions;

public interface IIntegrationEvent
{
    Guid Id { get; }
    DateTime OccurredAt { get; }
}

public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
