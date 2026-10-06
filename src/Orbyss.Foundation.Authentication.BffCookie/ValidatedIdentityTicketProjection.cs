using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Orbyss.Foundation.Authentication.Core;

namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>Owns the request-local validated projection through OIDC claim actions and ticket admission.</summary>
internal static class ValidatedIdentityTicketProjection
{
    /// <summary>Locates the admission witness only in its owning HTTP request.</summary>
    private static readonly object PendingProjectionKey = new();

    /// <summary>Replaces input projections with exact values from the successfully validated token.</summary>
    internal static bool Canonicalize(TokenValidatedContext context)
    {
        var identities = context.Principal?.Identities.Take(2).ToArray();
        if (identities is not { Length: 1 } || !identities[0].IsAuthenticated) return false;
        if (context.SecurityToken is null) return false;
        var issuer = SingleValue(context.SecurityToken.Claims, "iss");
        var subject = SingleValue(context.SecurityToken.Claims, "sub");
        if (issuer is null || subject is null || !issuer.Equals(context.SecurityToken.Issuer, StringComparison.Ordinal)
            || !issuer.Equals(SingleValue(identities[0].Claims, "iss"), StringComparison.Ordinal)
            || !subject.Equals(SingleValue(identities[0].Claims, "sub"), StringComparison.Ordinal)) return false;

        var identity = identities[0];
        foreach (var claim in identity.FindAll(AuthenticationClaimTypes.ValidatedIssuer)
            .Concat(identity.FindAll(AuthenticationClaimTypes.ValidatedSubject)).ToArray())
        {
            identity.RemoveClaim(claim);
        }
        var issuerClaim = new Claim(AuthenticationClaimTypes.ValidatedIssuer, issuer);
        var subjectClaim = new Claim(AuthenticationClaimTypes.ValidatedSubject, subject);
        identity.AddClaim(issuerClaim);
        identity.AddClaim(subjectClaim);
        // ClaimsIdentity.AddClaim clones claims whose Subject is not this identity.
        issuerClaim = identity.FindAll(AuthenticationClaimTypes.ValidatedIssuer).Single();
        subjectClaim = identity.FindAll(AuthenticationClaimTypes.ValidatedSubject).Single();
        context.HttpContext.Items[PendingProjectionKey] = new PendingValidatedIdentityProjection(
            new ValidatedAccountIdentity(issuer, subject), issuerClaim, subjectClaim);
        return true;
    }

    /// <summary>Rejects removal, replacement, reintroduction and ambiguity after native claim actions.</summary>
    internal static bool Admit(TicketReceivedContext context, IValidatedAccountIdentityReader reader)
    {
        if (!context.HttpContext.Items.Remove(PendingProjectionKey, out var state)
            || state is not PendingValidatedIdentityProjection expected || context.Principal is null
            || !reader.TryRead(context.Principal, out var identity) || identity != expected.Identity) return false;
        return ReferenceEquals(context.Principal.FindFirst(AuthenticationClaimTypes.ValidatedIssuer), expected.IssuerClaim)
            && ReferenceEquals(context.Principal.FindFirst(AuthenticationClaimTypes.ValidatedSubject), expected.SubjectClaim);
    }

    /// <summary>Requires one exact nonempty token/principal protocol claim.</summary>
    private static string? SingleValue(IEnumerable<Claim> claims, string type)
    {
        var selected = claims.Where(claim => claim.Type.Equals(type, StringComparison.Ordinal)).Take(2).ToArray();
        return selected.Length == 1 && !string.IsNullOrWhiteSpace(selected[0].Value) ? selected[0].Value : null;
    }
}
