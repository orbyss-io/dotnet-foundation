using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Describes a Foundation failure result; this public shape alone is not proof of bounded admission.</summary>
/// <remarks>Endpoint policy recognizes actual library-owned and native failure results through FoundationProblemResults.</remarks>
public interface IFoundationProblemResult : IResult, IStatusCodeHttpResult;
