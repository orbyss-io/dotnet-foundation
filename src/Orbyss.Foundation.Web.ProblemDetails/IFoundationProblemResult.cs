using Microsoft.AspNetCore.Http;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Identifies an admitted bounded Foundation failure result for endpoint response policy.</summary>
public interface IFoundationProblemResult : IResult, IStatusCodeHttpResult;
