using System.IO.Pipelines;
using Microsoft.AspNetCore.Http.Features;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Guards all native response-body entry points while an operation chooses its result.</summary>
internal sealed class JsonAdmissionResponseBodyFeature(IHttpResponseBodyFeature inner, JsonAdmissionState admission) : IHttpResponseBodyFeature
{
    /// <inheritdoc />
    public Stream Stream { get; } = new JsonAdmissionStream(inner.Stream, admission, request: false);
    /// <inheritdoc />
    public PipeWriter Writer { get; } = new JsonAdmissionPipeWriter(inner.Writer, admission);
    /// <inheritdoc />
    public void DisableBuffering() => inner.DisableBuffering();
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default) { admission.RequireWrite(); return inner.StartAsync(cancellationToken); }
    /// <inheritdoc />
    public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default)
    {
        admission.RequireWrite();
        throw new JsonResponseContractException(JsonFailureCodes.ResponseProfileBypass);
    }
    /// <inheritdoc />
    public Task CompleteAsync() { admission.RequireWrite(); return inner.CompleteAsync(); }
}
