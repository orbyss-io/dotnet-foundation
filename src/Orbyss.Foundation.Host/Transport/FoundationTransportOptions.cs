using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Orbyss.Foundation.Host.Transport;

/// <summary>Owns process-wide transport admission independently of shell JSON contracts and reverse proxies.</summary>
public sealed class FoundationTransportOptions
{
    /// <summary>Gets or sets the host body ceiling, preserving the native default until deployment selects another.</summary>
    public long MaxRequestBodyBytes { get; set; } = 30_000_000;
    /// <summary>Gets or sets maximum request headers independently of JSON bodies.</summary>
    public int MaxRequestHeadersBytes { get; set; } = 32 * 1024;
    /// <summary>Applies validated host limits before request processing begins.</summary>
    public void Apply(KestrelServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (MaxRequestBodyBytes is < 1 or > 1_073_741_824)
            throw new InvalidOperationException("Foundation:Transport:MaxRequestBodyBytes must be between 1 byte and 1 GiB.");
        if (MaxRequestHeadersBytes is < 1024 or > 1_048_576)
            throw new InvalidOperationException("Foundation:Transport:MaxRequestHeadersBytes must be between 1 KiB and 1 MiB.");
        options.Limits.MaxRequestBodySize = MaxRequestBodyBytes;
        options.Limits.MaxRequestHeadersTotalSize = MaxRequestHeadersBytes;
    }
}
