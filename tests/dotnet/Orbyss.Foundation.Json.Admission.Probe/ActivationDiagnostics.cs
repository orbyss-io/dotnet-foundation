using System.Collections.Concurrent;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Captures bounded local activation diagnostics, including CShells' absorbed pipeline failures.</summary>
internal sealed class ActivationDiagnostics : ILoggerProvider
{
    /// <summary>Retains bounded test classifications rather than depending on exception propagation.</summary>
    private readonly ConcurrentQueue<string> messages = new();
    /// <summary>Gets a stable view for the independent expected-rejection assertion.</summary>
    internal string Description => string.Join('\n', messages);
    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new ActivationDiagnosticLogger(messages);
    /// <inheritdoc />
    public void Dispose() { }
}
