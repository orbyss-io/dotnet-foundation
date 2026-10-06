using Microsoft.Extensions.Logging;

namespace Orbyss.Foundation.Host.Diagnostics;

/// <summary>Redacts the precise native request failure events that can bypass handled-exception policy.</summary>
internal sealed class FoundationRequestDiagnosticLogger(ILogger inner, string category) : ILogger
{
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);
    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);
    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (exception is null || !IsNativeRequestFailure(eventId.Id))
        {
            inner.Log(logLevel, eventId, state, exception, formatter);
            return;
        }
        // Do not call the original formatter or forward its state/exception: either may
        // expose private input, SQL, provider messages, data dictionaries or stack paths.
        var source = state as IEnumerable<KeyValuePair<string, object?>>;
        var safe = new Dictionary<string, object?>
        {
            [FoundationRequestDiagnosticConstants.FailureType] = AdmitIdentifier(exception.GetType().FullName),
            [FoundationRequestDiagnosticConstants.ConnectionId] = ReadIdentifier(source, FoundationRequestDiagnosticConstants.ConnectionId),
            [FoundationRequestDiagnosticConstants.CorrelationId] = ReadIdentifier(source, FoundationRequestDiagnosticConstants.NativeTraceIdentifier),
            [FoundationRequestDiagnosticConstants.OriginalFormat] = FoundationRequestDiagnosticConstants.FailureMessage
        };
        inner.Log(logLevel, eventId, safe, exception: null,
            static (values, _) => $"Framework request failure {values[FoundationRequestDiagnosticConstants.FailureType]}; connection {values[FoundationRequestDiagnosticConstants.ConnectionId]}; correlation {values[FoundationRequestDiagnosticConstants.CorrelationId]}.");
    }
    /// <summary>Identifies the audited ASP.NET10 native error events without suppressing whole categories.</summary>
    private bool IsNativeRequestFailure(int eventId) => category switch
    {
        FoundationRequestDiagnosticConstants.ExceptionHandlerCategory => eventId is FoundationRequestDiagnosticConstants.UnhandledException
            or FoundationRequestDiagnosticConstants.ErrorHandlerFailure,
        FoundationRequestDiagnosticConstants.DeveloperExceptionPageCategory => eventId is FoundationRequestDiagnosticConstants.UnhandledException
            or FoundationRequestDiagnosticConstants.ErrorHandlerFailure,
        FoundationRequestDiagnosticConstants.KestrelCategory => eventId is FoundationRequestDiagnosticConstants.KestrelApplicationFailure
            or FoundationRequestDiagnosticConstants.KestrelBodyDrainFailure,
        _ => false
    };
    /// <summary>Copies only an admitted framework connection/correlation identifier.</summary>
    private static string ReadIdentifier(IEnumerable<KeyValuePair<string, object?>>? values, string name) =>
        AdmitIdentifier(values?.FirstOrDefault(item => item.Key == name).Value as string);
    /// <summary>Excludes uncontrolled strings and keeps all retained diagnostic values finite.</summary>
    private static string AdmitIdentifier(string? value) => value is { Length: > 0 and <= 256 }
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-' or ':' or '+')
        ? value : "unavailable";
}
