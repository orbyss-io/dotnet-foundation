namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Declares the complete current and retained public deployment revisions.</summary>
public sealed class HostedPageManifest
{
    /// <summary>Gets or sets the supported manifest schema version.</summary>
    public required string Version { get; init; }
    /// <summary>Gets or sets the current revision, matching application settings.</summary>
    public required string CurrentRevision { get; init; }
    /// <summary>Gets or sets the explicit retained revision inventory.</summary>
    public required List<HostedPageRevision> Revisions { get; init; }
}
