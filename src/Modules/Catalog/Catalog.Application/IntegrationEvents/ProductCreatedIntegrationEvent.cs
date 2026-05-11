using BuildingBlocks.Application.Abstractions;

namespace Catalog.Application.IntegrationEvents;

public sealed record ProductCreatedIntegrationEvent(Guid ProductId, string Name, decimal Price) : IntegrationEvent;
