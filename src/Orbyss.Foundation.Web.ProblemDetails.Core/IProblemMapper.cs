namespace Orbyss.Foundation.Web.ProblemDetails.Core;

/// <summary>Maps an application-owned typed failure to safe bounded public text and diagnostics.</summary>
public interface IProblemMapper<in TFailure>
{
    /// <summary>Returns the immutable public definition; no exception or provider details are inferred.</summary>
    ProblemDefinition Map(TFailure failure);
}
