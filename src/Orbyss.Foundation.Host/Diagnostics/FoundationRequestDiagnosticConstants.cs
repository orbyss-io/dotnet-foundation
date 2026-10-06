namespace Orbyss.Foundation.Host.Diagnostics;

/// <summary>Owns the qualified ASP.NET10 logging event identities and admitted diagnostic fields.</summary>
internal static class FoundationRequestDiagnosticConstants
{
    /// <summary>The native exception handler logging category.</summary>
    public const string ExceptionHandlerCategory = "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware";
    /// <summary>The developer exception page logging category.</summary>
    public const string DeveloperExceptionPageCategory = "Microsoft.AspNetCore.Diagnostics.DeveloperExceptionPageMiddleware";
    /// <summary>The Kestrel general logging category.</summary>
    public const string KestrelCategory = "Microsoft.AspNetCore.Server.Kestrel";
    /// <summary>The native unhandled exception event.</summary>
    public const int UnhandledException = 1;
    /// <summary>The native error handler/page failure event.</summary>
    public const int ErrorHandlerFailure = 3;
    /// <summary>The Kestrel application exception event.</summary>
    public const int KestrelApplicationFailure = 13;
    /// <summary>The Kestrel failed request-body drain event.</summary>
    public const int KestrelBodyDrainFailure = 67;
    /// <summary>The safe runtime exception type classification key.</summary>
    public const string FailureType = "FailureType";
    /// <summary>The admitted connection identifier key.</summary>
    public const string ConnectionId = "ConnectionId";
    /// <summary>The native request identifier key before projection.</summary>
    public const string NativeTraceIdentifier = "TraceIdentifier";
    /// <summary>The canonical diagnostic request correlation key.</summary>
    public const string CorrelationId = "CorrelationId";
    /// <summary>The native structured-logging template key.</summary>
    public const string OriginalFormat = "{OriginalFormat}";
    /// <summary>The bounded structured event text without exception content.</summary>
    public const string FailureMessage = "Framework request failure {FailureType}; connection {ConnectionId}; correlation {CorrelationId}.";
}
