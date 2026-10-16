namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Optional pure page representation after native safe-problem admission.</summary>
public interface IProblemPageRenderer
{
    /// <summary>Computes encoded HTML within the response budget, or declines with null.</summary>
    /// <remarks>No response, exception, arbitrary extensions, request body or provider protocol is supplied.</remarks>
    string? Render(AdmittedProblemPage problem);
}
