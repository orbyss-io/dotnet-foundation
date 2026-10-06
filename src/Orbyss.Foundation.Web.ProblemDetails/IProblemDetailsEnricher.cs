using Microsoft.AspNetCore.Http;
using Orbyss.Foundation.Web.ProblemDetails.Core;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Enriches safe public presentation at request time without changing failure identity.</summary>
public interface IProblemDetailsEnricher
{
    /// <summary>Returns localized public text and bounded fields with the same status and code.</summary>
    ProblemDefinition Enrich(HttpContext context, ProblemDefinition definition);
}
