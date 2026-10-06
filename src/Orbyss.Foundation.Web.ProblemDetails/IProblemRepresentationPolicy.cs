using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Applies the shared bounded representation to ASP.NET's existing Problem Details writer.</summary>
public interface IProblemRepresentationPolicy
{
    /// <summary>Admits safe fields and resolves request-scoped contributions from RequestServices.</summary>
    void Apply(ProblemDetailsContext context);
}
