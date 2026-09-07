namespace Orbyss.Foundation.DomainEvents;

/// <summary>Publishes domain events to awaited handlers in the current process and scope.</summary>
public interface IDomainEventPublisher
{
    /// <summary>Publishes a business fact without providing durability or cross-process delivery.</summary>
    /// <typeparam name="TEvent">The domain-owned event type.</typeparam>
    /// <param name="domainEvent">The immutable domain event.</param>
    /// <param name="cancellationToken">Signals cancellation of the publishing operation.</param>
    /// <returns>A task that completes after every handler has completed.</returns>
    ValueTask PublishAsync<TEvent>(
        TEvent domainEvent,
        CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent;
}
