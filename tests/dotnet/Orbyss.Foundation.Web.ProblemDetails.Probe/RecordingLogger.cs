using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
internal sealed class RecordingLogger(ConcurrentBag<string> messages) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel level) => true;
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, exception) + exception?.ToString());
}
