using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Orbyss.Foundation.DomainEvents;

/// <summary>Dispatches handlers sequentially and propagates failures to the publishing operation.</summary>
internal sealed class DefaultDomainEventPublisher(
    IServiceProvider serviceProvider,
    DomainEventDispatchOptions options,
    TimeProvider timeProvider) : IDomainEventPublisher
{
    /// <summary>Emits one activity for every attempted publication.</summary>
    private static readonly ActivitySource ActivitySource = new("Orbyss.Foundation.DomainEvents");

    /// <summary>Flows nested publication metadata without introducing an ambient service locator.</summary>
    private readonly AsyncLocal<DomainEventDispatchState?> _currentDispatch = new();

    /// <inheritdoc />
    public async ValueTask PublishAsync<TEvent>(
        TEvent domainEvent,
        CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        cancellationToken.ThrowIfCancellationRequested();

        var ownsDispatch = _currentDispatch.Value is null;
        var state = _currentDispatch.Value ?? new DomainEventDispatchState(Guid.NewGuid());
        if (ownsDispatch)
            _currentDispatch.Value = state;

        var parentPublication = state.ActivePublicationId;
        state.Depth++;
        state.PublicationCount++;
        try
        {
            EnforceBounds(state);
            var publicationId = Guid.NewGuid();
            state.ActivePublicationId = publicationId;
            var context = new DomainEventContext(
                DispatchId: state.DispatchId,
                PublicationId: publicationId,
                CausationId: parentPublication,
                PublishedAt: timeProvider.GetUtcNow(),
                Depth: state.Depth);
            using var activity = StartActivity<TEvent>(context);
            foreach (var handler in serviceProvider.GetServices<IDomainEventHandler<TEvent>>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await handler.HandleAsync(domainEvent, context, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception error)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, error.Message);
            throw;
        }
        finally
        {
            state.ActivePublicationId = parentPublication;
            state.Depth--;
            if (ownsDispatch)
                _currentDispatch.Value = null;
        }
    }

    /// <summary>Rejects cycles or event storms before another handler is invoked.</summary>
    /// <param name="state">The active dispatch state.</param>
    private void EnforceBounds(DomainEventDispatchState state)
    {
        if (state.Depth > options.MaximumDepth)
            throw new InvalidOperationException(
                $"Domain-event dispatch {state.DispatchId} exceeded MaximumDepth {options.MaximumDepth}.");
        if (state.PublicationCount > options.MaximumPublications)
            throw new InvalidOperationException(
                $"Domain-event dispatch {state.DispatchId} exceeded MaximumPublications {options.MaximumPublications}.");
    }

    /// <summary>Starts one observable activity without changing event delivery semantics.</summary>
    /// <typeparam name="TEvent">The published event type.</typeparam>
    /// <param name="context">The publication context.</param>
    /// <returns>The activity when a listener is present; otherwise, <see langword="null"/>.</returns>
    private static Activity? StartActivity<TEvent>(DomainEventContext context)
        where TEvent : IDomainEvent
    {
        var activity = ActivitySource.StartActivity("domain-event.publish", ActivityKind.Internal);
        activity?.SetTag("domain_event.type", typeof(TEvent).FullName);
        activity?.SetTag("domain_event.dispatch_id", context.DispatchId);
        activity?.SetTag("domain_event.publication_id", context.PublicationId);
        activity?.SetTag("domain_event.causation_id", context.CausationId);
        activity?.SetTag("domain_event.depth", context.Depth);
        return activity;
    }
}
