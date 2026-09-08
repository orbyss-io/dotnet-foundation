using Orbyss.Foundation.Authentication;

namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Identifies the BFF-cookie authentication profile.</summary>
internal sealed class BffCookieProfileMarker : IFoundationAuthenticationProfile
{
    /// <inheritdoc />
    public string Name => "bff-cookie-v1";
}
