using System.Net;

/// <summary>Exposes an unknown-length stream and fails if HttpClient attempts automatic buffering.</summary>
internal sealed class StreamingOversizeContent : HttpContent
{
    /// <summary>Gets the generated response stream.</summary>
    internal CountingOversizeStream Stream { get; } = new();
    /// <summary>Gets whether automatic content buffering was attempted.</summary>
    internal bool BufferingAttempted { get; private set; }
    /// <inheritdoc />
    protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(Stream);
    /// <inheritdoc />
    protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) => Task.FromResult<Stream>(Stream);
    /// <inheritdoc />
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        BufferingAttempted = true;
        throw new InvalidOperationException("Automatic response buffering was attempted.");
    }
    /// <inheritdoc />
    protected override bool TryComputeLength(out long length) { length = 0; return false; }
}
