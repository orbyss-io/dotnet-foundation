namespace Orbyss.Foundation.Authentication.Core;

/// <summary>Reserved authentication projection claim types, compatible with existing BFF tickets.</summary>
public static class AuthenticationClaimTypes
{
    /// <summary>Identifies the token-validated issuer projection.</summary>
    public const string ValidatedIssuer = "urn:orbyss-foundation:authentication:validated-issuer";

    /// <summary>Identifies the token-validated subject projection.</summary>
    public const string ValidatedSubject = "urn:orbyss-foundation:authentication:validated-subject";
}
