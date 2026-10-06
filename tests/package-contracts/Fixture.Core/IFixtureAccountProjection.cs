using Orbyss.Foundation.Authentication.Core;
namespace Foundation.ContractFixture.Core;
public interface IFixtureAccountProjection
{
    FixtureAccountSnapshot Project(ValidatedAccountIdentity account);
}
