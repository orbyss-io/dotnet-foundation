using CShells;
using Microsoft.AspNetCore.Http;
using Orbyss.Foundation.Web.ProblemDetails;
using Orbyss.Foundation.Web.ProblemDetails.Core;
namespace Foundation.ContractFixture.Api;
internal sealed class FixtureProblemEnricher(ShellSettings settings) : IProblemDetailsEnricher
{
    private readonly Guid scopeId = Guid.NewGuid();
    public ProblemDefinition Enrich(HttpContext context, ProblemDefinition definition) => new(definition.StatusCode,
        definition.Code, settings.Id.ToString() + ":" + scopeId.ToString("N"), definition.Detail, definition.FieldErrors);
}
