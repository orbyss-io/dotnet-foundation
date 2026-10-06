using Microsoft.AspNetCore.Http;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;
internal sealed class ProbeEnricher(EnrichmentLedger ledger) : IProblemDetailsEnricher
{
    private readonly Guid scopeIdentity = Guid.NewGuid();
    public ProblemDefinition Enrich(HttpContext context, ProblemDefinition definition)
    {
        ledger.Calls.AddOrUpdate(context.TraceIdentifier, 1, (_, count) => count + 1);
        ledger.ScopeIdentities.Add(scopeIdentity);
        if (definition.Code == "maximum") return definition;
        if (definition.Code == "unexpected_cancellation")
            throw new OperationCanceledException("PRIVATE unrelated cancellation", new CancellationToken(canceled: true));
        if (definition.Code == "change_reserved") return new ProblemDefinition(400, "replacement");
        return new ProblemDefinition(definition.StatusCode, definition.Code, "Veilige fout", definition.Detail,
            definition.FieldErrors);
    }
}
