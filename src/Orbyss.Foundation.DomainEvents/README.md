# Orbyss.Foundation.DomainEvents

This package supplies Orbyss Foundation's default awaited in-process dispatcher. Activate the
`Orbyss.Foundation.DomainEvents` feature in each shell that publishes or handles domain events.

Handler-owning features reference only `Orbyss.Foundation.DomainEvents.Abstractions` and register their
handlers through their normal feature service collection:

```csharp
services.AddScoped<IDomainEventHandler<CatalogRevisionPublished>, RefreshCatalogProjection>();
```

They do not reference this concrete package; `shells.json` activates it independently.

The dispatcher resolves scoped typed handlers, invokes them sequentially, propagates cancellation
and failures, carries separate technical publication metadata, instruments each publication, and
bounds nested publication depth and count. Handler registration order is not a supported business
ordering contract.

This implementation is deliberately non-durable. Publishing after a state commit can be lost if
the process terminates. Reliable post-commit, background, broker, or cross-process delivery requires
the separately governed Integration Events capability and an atomic outbox; do not simulate it with
fire-and-forget domain-event handlers.
