using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Orbyss.Foundation.Authentication.Core;

namespace Orbyss.Foundation.Authentication;

/// <summary>Reads only unambiguous projections; it never chooses a first identity or first claim.</summary>
internal sealed class ValidatedAccountIdentityReader : IValidatedAccountIdentityReader
{
    /// <inheritdoc />
    public bool TryRead(ClaimsPrincipal principal, [NotNullWhen(true)] out ValidatedAccountIdentity? identity)
    {
        ArgumentNullException.ThrowIfNull(principal);
        identity = null;
        var identities = principal.Identities.Take(2).ToArray();
        if (identities.Length != 1 || !identities[0].IsAuthenticated) return false;
        var issuer = SingleValue(identities[0], AuthenticationClaimTypes.ValidatedIssuer);
        var subject = SingleValue(identities[0], AuthenticationClaimTypes.ValidatedSubject);
        if (issuer is null || subject is null) return false;
        identity = new ValidatedAccountIdentity(issuer, subject);
        return true;
    }

    /// <summary>Rejects absent, duplicate and empty reserved values.</summary>
    private static string? SingleValue(ClaimsIdentity identity, string type)
    {
        var claims = identity.FindAll(type).Take(2).ToArray();
        return claims.Length == 1 && claims[0].Type.Equals(type, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(claims[0].Value) ? claims[0].Value : null;
    }
}
