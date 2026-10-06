using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
internal sealed class DecliningHandler(DispatchLedger ledger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ledger.Declined);
        return ValueTask.FromResult(false);
    }
}
