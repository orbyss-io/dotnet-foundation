using Microsoft.AspNetCore.Http;
using Orbyss.Foundation.Web.ProblemDetails.Core;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Adapts admitted application definitions to the existing ASP.NET Problem Details result.</summary>
public static class FoundationProblemResults
{
    /// <summary>Creates a failure result; serialization and DI enrichment remain ASP.NET-owned.</summary>
    public static IResult Problem(ProblemDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = definition.StatusCode,
            Title = definition.Title,
            Detail = definition.Detail
        };
        problem.Extensions[ProblemExtensionNames.Code] = definition.Code;
        problem.Extensions[ProblemExtensionNames.FieldErrors] = definition.FieldErrors;
        return new FoundationProblemResult(problem);
    }
    /// <summary>Creates a failure result through an application-owned typed mapping.</summary>
    public static IResult Problem<TFailure>(TFailure failure, IProblemMapper<TFailure> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return Problem(mapper.Map(failure));
    }
    /// <summary>Maps an HTTP status to the preserved 0.2.4 generic code.</summary>
    public static string CodeForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status401Unauthorized => ProblemCodes.AuthenticationRequired,
        StatusCodes.Status403Forbidden => ProblemCodes.AuthorizationDenied,
        StatusCodes.Status400BadRequest => ProblemCodes.InvalidRequest,
        _ => ProblemCodes.RequestFailed
    };
}
