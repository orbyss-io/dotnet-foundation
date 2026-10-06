using System.Net;
using System.Net.Http.Headers;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Produces real chunked requests without an advertised content length.</summary>
internal sealed class ChunkedContent(byte[] payload, bool pauseAfterPrefix = false) : HttpContent
{
    /// <summary>Uses a JSON media type while deliberately declining length calculation.</summary>
    internal void Configure() => Headers.ContentType = new MediaTypeHeaderValue("application/json");
    /// <inheritdoc />
    protected override bool TryComputeLength(out long length) { length = 0; return false; }
    /// <inheritdoc />
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);
    /// <inheritdoc />
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < payload.Length; offset += 7)
        {
            await stream.WriteAsync(payload.AsMemory(offset, Math.Min(7, payload.Length - offset)), cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        if (pauseAfterPrefix) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}
