using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;
internal sealed class ConflictHandler(DispatchLedger ledger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken cancellationToken)
    {
        if (error is not ProbeConflictException) return false;
        Interlocked.Increment(ref ledger.ConflictHandled);
        await FoundationProblemResults.Problem(new ProblemDefinition(409, "revision_conflict")).ExecuteAsync(context);
        return true;
    }
}
