using System.IO.Pipelines;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Rejects native pipe reservations until typed response output is admitted.</summary>
internal sealed class JsonAdmissionPipeWriter(PipeWriter inner, JsonAdmissionState admission) : PipeWriter
{
    /// <inheritdoc />
    public override void Advance(int bytes) { admission.RequireWrite(); inner.Advance(bytes); }
    /// <inheritdoc />
    public override void CancelPendingFlush() => inner.CancelPendingFlush();
    /// <inheritdoc />
    public override void Complete(Exception? exception = null) { admission.RequireWrite(); inner.Complete(exception); }
    /// <inheritdoc />
    public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default) { admission.RequireWrite(); return inner.FlushAsync(cancellationToken); }
    /// <inheritdoc />
    public override Memory<byte> GetMemory(int sizeHint = 0) { admission.RequireWrite(); return inner.GetMemory(sizeHint); }
    /// <inheritdoc />
    public override Span<byte> GetSpan(int sizeHint = 0) { admission.RequireWrite(); return inner.GetSpan(sizeHint); }
}
