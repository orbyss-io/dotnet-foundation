using System.Security.Claims;
using Orbyss.Foundation.Authentication.Core;

namespace Orbyss.Foundation.Authentication.BffCookie;

/// <summary>A request-local witness retaining the original canonical claims through native claim actions.</summary>
/// <param name="Identity">The validated token account identity.</param>
/// <param name="IssuerClaim">The exact issuer projection claim instance.</param>
/// <param name="SubjectClaim">The exact subject projection claim instance.</param>
internal sealed record PendingValidatedIdentityProjection(ValidatedAccountIdentity Identity, Claim IssuerClaim, Claim SubjectClaim);
