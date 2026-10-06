using Microsoft.Extensions.Logging;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>Preserves native diagnostic classification without forwarding SQL, parameters or raw exceptions.</summary>
internal sealed class PostgreSqlRedactingLoggerFactory(ILoggerFactory destination) : ILoggerFactory
{
    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new PostgreSqlRedactingLogger(destination.CreateLogger(categoryName));

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException("Native PostgreSQL diagnostics use the application-owned logging factory.");

    /// <inheritdoc />
    public void Dispose() { }

}
