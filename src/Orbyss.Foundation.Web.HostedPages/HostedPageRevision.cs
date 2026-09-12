namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Declares public page content and one trusted Vite runtime deployment.</summary>
public sealed class HostedPageRevision
{
    /// <summary>Gets the immutable product revision identity.</summary>
    public required string Id { get; init; }
    /// <summary>Gets the public Forms release identity passed to the admitted runtime.</summary>
    public required string FormReleaseId { get; init; }
    /// <summary>Gets the deterministic language fallback.</summary>
    public required string DefaultLocale { get; init; }
    /// <summary>Gets localized public text; there are no supplier or private fields.</summary>
    public required Dictionary<string, HostedBrandLocale> Locales { get; init; }
    /// <summary>Gets an optional admitted branding asset identity.</summary>
    public string? LogoAssetId { get; init; }
    /// <summary>Gets the constrained built-in theme token.</summary>
    public string Theme { get; init; } = "blue";
    /// <summary>Gets the selected Vite manifest key.</summary>
    public required string Entry { get; init; }
    /// <summary>Gets the typed Vite manifest including all recursively imported chunks.</summary>
    public required Dictionary<string, HostedViteChunk> Vite { get; init; }
    /// <summary>Gets the explicitly public file inventory.</summary>
    public required List<HostedAssetDescriptor> Assets { get; init; }
}
