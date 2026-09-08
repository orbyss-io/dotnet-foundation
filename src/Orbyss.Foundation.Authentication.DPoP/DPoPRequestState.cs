namespace Orbyss.Foundation.Authentication.DPoP;

/// <summary>Names private request items shared between DPoP normalization and token validation.</summary>
internal static class DPoPRequestState
{
    /// <summary>Stores the original DPoP-scheme access token.</summary>
    internal const string AccessToken = "Orbyss.Foundation.Authentication.DPoP.AccessToken";

    /// <summary>Stores the request's unique DPoP proof.</summary>
    internal const string Proof = "Orbyss.Foundation.Authentication.DPoP.Proof";
}
