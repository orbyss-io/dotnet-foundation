# Orbyss.Foundation.DomainEvents.Abstractions

This package defines awaited, typed, in-process domain-event publication for Orbyss Foundation features.
Domain-owned `.Core` projects may reference it without depending on the dispatcher implementation,
CShells, persistence, transport, or a message broker.

Domain events are immutable past-tense business facts. They are not durable integration messages.
Do not use this package to promise post-commit, cross-process, retryable, or exactly-once delivery.
Those requirements belong to the separately governed Integration Events capability and a durable
outbox implementation.

Activatable packages implement the relevant `IDomainEventHandler<TEvent>` contracts and register
them with their own feature composition. They do not reference the concrete dispatcher package;
`shells.json` activates that runtime implementation independently.

Handlers must be independent. Registration order is not a business workflow contract; use an
explicit orchestrator when actions require ordering, returned results, compensation, or lifecycle
state.
