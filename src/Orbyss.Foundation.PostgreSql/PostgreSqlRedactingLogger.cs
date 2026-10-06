using Microsoft.Extensions.Logging;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>Forwards only provider-owned safe fields; ambient application correlation stays in the destination.</summary>
internal sealed class PostgreSqlRedactingLogger(ILogger destinationLogger) : ILogger
{
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => destinationLogger.IsEnabled(logLevel);

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var sqlState = exception is not null && PostgreSqlFailureInfo.TryRead(exception, out var failure) ? failure!.SqlState : null;
        destinationLogger.Log(logLevel, eventId,
            "PostgreSQL native diagnostic {NativeEventId}; failure {FailureType}; SQLSTATE {SqlState}.",
            eventId.Id, exception?.GetType().Name, sqlState);
    }
}
