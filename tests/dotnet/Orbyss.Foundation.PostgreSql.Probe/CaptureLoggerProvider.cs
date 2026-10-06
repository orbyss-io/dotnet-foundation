using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

internal sealed class CaptureLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Entries { get; } = new();
    public ILogger CreateLogger(string categoryName) => new CaptureLogger(this, categoryName);
    public void Dispose() { }
    private sealed class CaptureLogger(CaptureLoggerProvider owner, string category) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            if (category.StartsWith("Npgsql", StringComparison.Ordinal) || category.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
                owner.Entries.Enqueue("scope:" + state);
            return null;
        }
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (category.StartsWith("Npgsql", StringComparison.Ordinal) || category.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
                owner.Entries.Enqueue(category + "|" + logLevel + "|" + eventId.Id + "|" + formatter(state, exception) + (exception is null ? "" : "\n" + exception));
        }
    }
}
