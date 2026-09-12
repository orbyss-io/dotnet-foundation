namespace Orbyss.Foundation.WebDefaults;

/// <summary>Holds one immutable validated response policy.</summary>
/// <param name="ContentSecurityPolicy">Validated CSP.</param>
/// <param name="ReferrerPolicy">Validated referrer policy.</param>
/// <param name="PermissionsPolicy">Validated capabilities policy.</param>
/// <param name="PublicAssetMaxAgeSeconds">Public immutable asset freshness.</param>
/// <param name="AllowIndexing">Whether indexing is permitted.</param>
public sealed record WebResponsePolicy(string ContentSecurityPolicy, string ReferrerPolicy,
    string PermissionsPolicy, int PublicAssetMaxAgeSeconds, bool AllowIndexing);
