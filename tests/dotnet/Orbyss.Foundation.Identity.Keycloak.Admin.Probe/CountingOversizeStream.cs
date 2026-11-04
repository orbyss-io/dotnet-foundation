/// <summary>Generates an oversized response incrementally without allocating its payload.</summary>
internal sealed class CountingOversizeStream : Stream
{
    /// <summary>Gets the number of bytes actually requested by the consumer.</summary>
    internal long BytesRead { get; private set; }
    /// <inheritdoc />
    public override bool CanRead => true;
    /// <inheritdoc />
    public override bool CanSeek => false;
    /// <inheritdoc />
    public override bool CanWrite => false;
    /// <inheritdoc />
    public override long Length => 16_777_216;
    /// <inheritdoc />
    public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        var admitted = (int)Math.Min(count, Length - BytesRead);
        buffer.AsSpan(offset, admitted).Fill((byte)'x');
        BytesRead += admitted;
        return admitted;
    }
    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var admitted = (int)Math.Min(buffer.Length, Length - BytesRead);
        buffer.Span[..admitted].Fill((byte)'x');
        BytesRead += admitted;
        return ValueTask.FromResult(admitted);
    }
    /// <inheritdoc />
    public override void Flush() => throw new NotSupportedException();
    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
