namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Records actual native request-scope disposal for the endpoint registration control.</summary>
internal sealed class ScopedEndpointControl : IDisposable
{
    /// <summary>Counts completed scope-owned disposal.</summary>
    internal static int Disposed;

    /// <inheritdoc />
    public void Dispose() => Interlocked.Increment(ref Disposed);
}
