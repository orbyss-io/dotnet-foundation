using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;
namespace Foundation.ContractFixture.Api;
internal sealed class FixtureConflictExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken cancellationToken)
    {
        if (error is not FixtureConflictException || context.Response.HasStarted || cancellationToken.IsCancellationRequested) return false;
        await FoundationProblemResults.Problem(new ProblemDefinition(409, FixtureApiKeys.NativeConflictCode)).ExecuteAsync(context);
        return true;
    }
}
