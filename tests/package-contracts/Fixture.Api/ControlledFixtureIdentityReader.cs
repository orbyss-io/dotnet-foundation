using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using CShells;
using Orbyss.Foundation.Authentication;
using Orbyss.Foundation.Authentication.Core;
namespace Foundation.ContractFixture.Api;
// This deliberate controlled replacement is qualification-only, not a production authentication policy.
internal sealed class ControlledFixtureIdentityReader(ShellSettings settings) : IValidatedAccountIdentityReader
{
    public bool TryRead(ClaimsPrincipal principal, [NotNullWhen(true)] out ValidatedAccountIdentity? identity)
    {
        var identities = principal.Identities.Take(2).ToArray();
        identity = identities.Length == 1 && identities[0].IsAuthenticated
            ? new ValidatedAccountIdentity("replacement-fixture", settings.Id.ToString() + "-replacement") : null;
        return identity is not null;
    }
}
