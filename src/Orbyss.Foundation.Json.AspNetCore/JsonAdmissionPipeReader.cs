using System.Buffers;
using System.IO.Pipelines;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Guards direct request pipe access against bypassing the registered parser.</summary>
internal sealed class JsonAdmissionPipeReader(PipeReader inner, JsonAdmissionState admission) : PipeReader
{
    /// <inheritdoc />
    public override void AdvanceTo(SequencePosition consumed) { admission.RequireRead(); inner.AdvanceTo(consumed); }
    /// <inheritdoc />
    public override void AdvanceTo(SequencePosition consumed, SequencePosition examined) { admission.RequireRead(); inner.AdvanceTo(consumed, examined); }
    /// <inheritdoc />
    public override void CancelPendingRead() => inner.CancelPendingRead();
    /// <inheritdoc />
    public override void Complete(Exception? exception = null) { admission.RequireRead(); inner.Complete(exception); }
    /// <inheritdoc />
    public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default) { admission.RequireRead(); return inner.ReadAsync(cancellationToken); }
    /// <inheritdoc />
    public override bool TryRead(out ReadResult result) { admission.RequireRead(); return inner.TryRead(out result); }
}
