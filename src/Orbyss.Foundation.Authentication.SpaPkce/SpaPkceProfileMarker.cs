using Orbyss.Foundation.Authentication;

namespace Orbyss.Foundation.Authentication.SpaPkce;

/// <summary>Identifies the direct SPA-PKCE authentication profile.</summary>
internal sealed class SpaPkceProfileMarker : IFoundationAuthenticationProfile
{
    /// <inheritdoc />
    public string Name => "spa-pkce-v1";
}
