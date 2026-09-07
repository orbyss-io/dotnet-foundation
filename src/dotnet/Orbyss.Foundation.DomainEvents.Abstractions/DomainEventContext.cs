namespace Orbyss.Foundation.DomainEvents;

/// <summary>Provides delivery metadata without contaminating the domain-owned event contract.</summary>
/// <param name="DispatchId">Identifies the complete top-level dispatch including nested events.</param>
/// <param name="PublicationId">Identifies this individual publication.</param>
/// <param name="CausationId">Identifies the publication whose handler emitted this nested event.</param>
/// <param name="PublishedAt">Records when in-process publication started.</param>
/// <param name="Depth">Records zero-based nested publication depth.</param>
public sealed record DomainEventContext(
    Guid DispatchId,
    Guid PublicationId,
    Guid? CausationId,
    DateTimeOffset PublishedAt,
    int Depth);
