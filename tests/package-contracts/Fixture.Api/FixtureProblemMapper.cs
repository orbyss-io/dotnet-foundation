using Foundation.ContractFixture.Core;
using Orbyss.Foundation.Web.ProblemDetails.Core;
namespace Foundation.ContractFixture.Api;
internal sealed class FixtureProblemMapper : IProblemMapper<FixtureDenied>
{
    public ProblemDefinition Map(FixtureDenied failure) => new(409, FixtureApiKeys.ConflictCode,
        fieldErrors: [new ProblemFieldError("name", "conflict", "Conflict.")]);
}
