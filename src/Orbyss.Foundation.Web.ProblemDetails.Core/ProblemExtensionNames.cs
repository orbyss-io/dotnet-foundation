namespace Orbyss.Foundation.Web.ProblemDetails.Core;

/// <summary>Owns the stable shared problem envelope extension names.</summary>
public static class ProblemExtensionNames
{
    /// <summary>The application or managed-boundary problem code.</summary>
    public const string Code = "code";
    /// <summary>The canonical request correlation identifier.</summary>
    public const string CorrelationId = "correlationId";
    /// <summary>The retained 0.2.4 compatibility alias of correlationId.</summary>
    public const string TraceId = "traceId";
    /// <summary>The bounded public field diagnostics.</summary>
    public const string FieldErrors = "fieldErrors";
}
