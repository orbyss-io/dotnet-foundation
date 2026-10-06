using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    public ConcurrentBag<string> Messages { get; } = new();
    public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages);
    public void Dispose() { }
}
