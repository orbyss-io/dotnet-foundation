namespace Orbyss.Foundation.WebDefaults;

/// <summary>Selects a complete policy and describes explicit resource admission.</summary>
/// <param name="Policy">Optional named endpoint policy.</param>
/// <param name="Feature">Optional owning feature identity for settings selection.</param>
/// <param name="Private">Whether this response must never be publicly cached or indexed.</param>
/// <param name="ImmutablePublicAsset">Whether immutable public content was explicitly admitted.</param>
/// <param name="NoIndex">Whether resource-specific indexing is forbidden.</param>
public sealed record WebResponseMetadata(string? Policy = null, string? Feature = null,
    bool Private = false, bool ImmutablePublicAsset = false, bool NoIndex = false);
