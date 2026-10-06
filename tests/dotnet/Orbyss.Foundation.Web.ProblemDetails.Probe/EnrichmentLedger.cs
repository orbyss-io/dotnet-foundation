using System.Collections.Concurrent;
internal sealed class EnrichmentLedger
{
    public ConcurrentDictionary<string, int> Calls { get; } = new();
    public ConcurrentBag<Guid> ScopeIdentities { get; } = new();
}
