using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Retains the native prefix before CShells rejoins the outer pipeline.</summary>
internal sealed record ProblemRequestPathBase(PathString Value);
