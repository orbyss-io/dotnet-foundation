using Foundation.ContractFixture.Core;
using Orbyss.Foundation.Authentication.Core;
using Orbyss.Foundation.Collections.Core;
namespace Foundation.ContractFixture;
internal sealed class FixtureAccountProjection(FixtureGeneration generation) : IFixtureAccountProjection
{
    private readonly Guid scopeId = Guid.NewGuid();
    public FixtureAccountSnapshot Project(ValidatedAccountIdentity account) => new(account.Issuer, account.Subject,
        new ValueSequence<string>([generation.ShellName, generation.Id.ToString("N"), scopeId.ToString("N")]));
}
