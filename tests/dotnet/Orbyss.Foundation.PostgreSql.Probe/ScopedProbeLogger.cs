using Microsoft.Extensions.Logging;

internal sealed class ScopedProbeLogger<T>(ILoggerFactory factory) : ILogger<T>, IAsyncDisposable
{
    private readonly ILogger logger = factory.CreateLogger(typeof(T).FullName!);
    private readonly ScopeDisposalControl? control = typeof(T).Name.StartsWith("OwnedPostgreSqlDbContextFactory", StringComparison.Ordinal)
        ? ScopeDisposalControl.Claim() : null;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => logger.BeginScope(state);
    public bool IsEnabled(LogLevel logLevel) => logger.IsEnabled(logLevel);
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => logger.Log(logLevel, eventId, state, exception, formatter);
    public ValueTask DisposeAsync() => control?.DisposeAsync() ?? ValueTask.CompletedTask;
}
