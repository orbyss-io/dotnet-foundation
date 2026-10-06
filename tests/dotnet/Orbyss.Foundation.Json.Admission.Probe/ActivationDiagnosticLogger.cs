using System.Collections.Concurrent;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Records deterministic activation diagnostics from the real shell pipeline.</summary>
internal sealed class ActivationDiagnosticLogger(ConcurrentQueue<string> messages) : ILogger
{
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;
    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (messages.Count >= 128) return;
        var text = formatter(state, exception) + (exception is null ? "" : "\n" + exception);
        messages.Enqueue(text[..Math.Min(text.Length, 8192)]);
    }
}
