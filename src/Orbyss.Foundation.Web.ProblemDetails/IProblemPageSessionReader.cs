using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Native authentication contribution; the renderer receives only its immutable bounded state.</summary>
public interface IProblemPageSessionReader
{
    /// <summary>Returns independently observed local authentication, or null when no native observation exists.</summary>
    bool? ReadLocalSession(HttpContext context);
}
