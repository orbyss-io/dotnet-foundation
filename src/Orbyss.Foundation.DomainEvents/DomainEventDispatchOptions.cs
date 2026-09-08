namespace Orbyss.Foundation.DomainEvents;

/// <summary>Bounds nested in-process event publication without defining business ordering.</summary>
public sealed class DomainEventDispatchOptions
{
    /// <summary>Gets or sets the maximum zero-based nested publication depth.</summary>
    public int MaximumDepth { get; set; } = 32;

    /// <summary>Gets or sets the maximum number of publications in one top-level dispatch.</summary>
    public int MaximumPublications { get; set; } = 1024;

    /// <summary>Rejects unsafe dispatch bounds during feature activation.</summary>
    public void Validate()
    {
        if (MaximumDepth < 0)
            throw new InvalidOperationException("Domain-event MaximumDepth cannot be negative.");
        if (MaximumPublications < 1)
            throw new InvalidOperationException("Domain-event MaximumPublications must be positive.");
    }
}
