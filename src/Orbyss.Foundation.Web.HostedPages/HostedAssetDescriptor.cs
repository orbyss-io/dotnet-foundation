namespace Orbyss.Foundation.Web.HostedPages;

/// <summary>Declares one public asset; authored branding and trusted executable runtime files are distinct.</summary>
public sealed class HostedAssetDescriptor
{
    /// <summary>Gets an opaque public asset identity.</summary>
    public required string Id { get; init; }
    /// <summary>Gets its literal root-relative deployment path.</summary>
    public required string File { get; init; }
    /// <summary>Gets runtime or branding trust classification.</summary>
    public required string Kind { get; init; }
    /// <summary>Gets the explicit public visibility marker.</summary>
    public required string Visibility { get; init; }
    /// <summary>Gets the admitted media type.</summary>
    public required string ContentType { get; init; }
    /// <summary>Gets the expected SHA-256 of exact served bytes.</summary>
    public required string Sha256 { get; init; }
}
