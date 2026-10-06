namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Rejects direct operation I/O before its registered typed parser/result has admitted it.</summary>
internal sealed class JsonAdmissionStream(Stream inner, JsonAdmissionState admission, bool request) : Stream
{
    /// <inheritdoc />
    public override bool CanRead => request && inner.CanRead;
    /// <inheritdoc />
    public override bool CanWrite => !request && inner.CanWrite;
    /// <inheritdoc />
    public override bool CanSeek => false;
    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();
    /// <inheritdoc />
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    /// <inheritdoc />
    public override void Flush() { admission.RequireWrite(); inner.Flush(); }
    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) { admission.RequireWrite(); return inner.FlushAsync(cancellationToken); }
    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) { admission.RequireRead(); return inner.Read(buffer, offset, count); }
    /// <inheritdoc />
    public override int Read(Span<byte> buffer) { admission.RequireRead(); return inner.Read(buffer); }
    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { admission.RequireRead(); return inner.ReadAsync(buffer, cancellationToken); }
    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) { admission.RequireRead(); return inner.ReadAsync(buffer, offset, count, cancellationToken); }
    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) { admission.RequireWrite(); inner.Write(buffer, offset, count); }
    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer) { admission.RequireWrite(); inner.Write(buffer); }
    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) { admission.RequireWrite(); return inner.WriteAsync(buffer, cancellationToken); }
    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) { admission.RequireWrite(); return inner.WriteAsync(buffer, offset, count, cancellationToken); }
    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();
}
