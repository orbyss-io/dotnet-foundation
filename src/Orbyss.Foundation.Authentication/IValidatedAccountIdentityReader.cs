using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Orbyss.Foundation.Authentication.Core;

namespace Orbyss.Foundation.Authentication;

/// <summary>Admits one authenticated principal into the validated account projection contract.</summary>
public interface IValidatedAccountIdentityReader
{
    /// <summary>Requires exactly one identity and one nonempty reserved issuer-subject claim pair.</summary>
    /// <param name="principal">The principal produced by the selected authentication handler.</param>
    /// <param name="identity">The admitted account projection, or null on rejection.</param>
    /// <returns>Whether the principal has an unambiguous authenticated projection.</returns>
    bool TryRead(ClaimsPrincipal principal, [NotNullWhen(true)] out ValidatedAccountIdentity? identity);
}
