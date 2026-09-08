namespace Orbyss.Foundation.DomainEvents;

/// <summary>Tracks one awaited top-level dispatch and its nested publications.</summary>
internal sealed class DomainEventDispatchState(Guid dispatchId)
{
    /// <summary>Gets the top-level dispatch identifier.</summary>
    public Guid DispatchId { get; } = dispatchId;

    /// <summary>Gets or sets the active publication identifier used as nested causation.</summary>
    public Guid? ActivePublicationId { get; set; }

    /// <summary>Gets or sets the current nested publication depth.</summary>
    public int Depth { get; set; } = -1;

    /// <summary>Gets or sets the total publications attempted by this dispatch.</summary>
    public int PublicationCount { get; set; }
}
