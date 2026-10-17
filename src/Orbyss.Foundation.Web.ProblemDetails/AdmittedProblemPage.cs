namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Immutable safe fields admitted by the native policy and bounded wire encoder.</summary>
public sealed class AdmittedProblemPage
{
    /// <summary>Constructs the page input only after closed-envelope admission succeeds.</summary>
    internal AdmittedProblemPage(int status, string code, string title, string correlationId, string applicationPathBase = "", bool? hasLocalSession = null)
    {
        Status = status;
        Code = code;
        Title = title;
        CorrelationId = correlationId;
        ApplicationPathBase = applicationPathBase;
        HasLocalSession = hasLocalSession;
    }
    /// <summary>Gets the reserved HTTP failure status.</summary>
    public int Status { get; }
    /// <summary>Gets the reserved stable failure code.</summary>
    public string Code { get; }
    /// <summary>Gets the admitted display title; HTML renderers must encode it.</summary>
    public string Title { get; }
    /// <summary>Gets the bounded correlation token, without request or exception text.</summary>
    public string CorrelationId { get; }
    /// <summary>Gets the native application prefix admitted without query, fragment, escapes or control text.</summary>
    public string ApplicationPathBase { get; }
    /// <summary>Gets an independent native local-session observation; null means unobserved, never signed out.</summary>
    public bool? HasLocalSession { get; }
}
