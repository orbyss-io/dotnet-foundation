namespace Orbyss.Foundation.WebDefaults;

/// <summary>Owns one lazily generated style nonce and its immutable shell catalog.</summary>
internal sealed class WebResponseNonceState(WebResponsePolicyCatalog catalog)
{
    /// <summary>Gets the exact immutable shell catalog.</summary>
    public WebResponsePolicyCatalog Catalog { get; } = catalog;
    /// <summary>Gets or sets the Foundation-created value for this response.</summary>
    public string? Nonce { get; set; }
}
