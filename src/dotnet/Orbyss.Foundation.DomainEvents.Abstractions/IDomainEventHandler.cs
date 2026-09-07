namespace Orbyss.Foundation.DomainEvents;

/// <summary>Handles one in-process domain-event type.</summary>
/// <typeparam name="TEvent">The domain-owned event type.</typeparam>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    /// <summary>Handles a published business fact in the current operation scope.</summary>
    /// <param name="domainEvent">The immutable domain event.</param>
    /// <param name="context">Technical metadata for this publication.</param>
    /// <param name="cancellationToken">Signals cancellation of the publishing operation.</param>
    /// <returns>A task that completes when handling has finished.</returns>
    ValueTask HandleAsync(
        TEvent domainEvent,
        DomainEventContext context,
        CancellationToken cancellationToken);
}
