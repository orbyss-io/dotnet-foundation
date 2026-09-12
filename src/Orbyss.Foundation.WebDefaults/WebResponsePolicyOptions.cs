namespace Orbyss.Foundation.WebDefaults;

/// <summary>Defines a complete named response policy; values are copied at shell activation.</summary>
public sealed class WebResponsePolicyOptions
{
    /// <summary>Gets or sets the CSP. Framing and object restrictions are mandatory.</summary>
    public string ContentSecurityPolicy { get; set; } = "default-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'self'";
    /// <summary>Gets or sets the browser referrer policy.</summary>
    public string ReferrerPolicy { get; set; } = "no-referrer";
    /// <summary>Gets or sets the browser capabilities policy.</summary>
    public string PermissionsPolicy { get; set; } = "camera=(), microphone=(), geolocation=()";
    /// <summary>Gets or sets public asset freshness; zero means no-store.</summary>
    public int PublicAssetMaxAgeSeconds { get; set; }
    /// <summary>Gets or sets whether search indexing is permitted.</summary>
    public bool AllowIndexing { get; set; }
}
