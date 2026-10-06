using Microsoft.Extensions.Logging;

namespace Orbyss.Foundation.Host.Diagnostics;

/// <summary>Retains the normal host logging providers and owns only the factory it created.</summary>
internal sealed class FoundationRequestDiagnosticLoggerFactory(ILoggerFactory inner, bool ownsInner) : ILoggerFactory
{
    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new FoundationRequestDiagnosticLogger(inner.CreateLogger(categoryName), categoryName);
    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider) => inner.AddProvider(provider);
    /// <inheritdoc />
    public void Dispose()
    {
        if (ownsInner) inner.Dispose();
    }
}
