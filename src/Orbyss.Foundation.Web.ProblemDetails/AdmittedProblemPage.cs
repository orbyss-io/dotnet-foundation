namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Immutable safe fields admitted by the native policy and bounded wire encoder.</summary>
public sealed class AdmittedProblemPage
{
    /// <summary>Constructs the page input only after closed-envelope admission succeeds.</summary>
    internal AdmittedProblemPage(int status, string code, string title, string correlationId)
    {
        Status = status;
        Code = code;
        Title = title;
        CorrelationId = correlationId;
    }
    /// <summary>Gets the reserved HTTP failure status.</summary>
    public int Status { get; }
    /// <summary>Gets the reserved stable failure code.</summary>
    public string Code { get; }
    /// <summary>Gets the admitted display title; HTML renderers must encode it.</summary>
    public string Title { get; }
    /// <summary>Gets the bounded correlation token, without request or exception text.</summary>
    public string CorrelationId { get; }
}
