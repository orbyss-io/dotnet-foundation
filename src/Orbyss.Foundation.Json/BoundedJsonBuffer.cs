using System.Buffers;

namespace Orbyss.Foundation.Json;

/// <summary>Separates finite serializer scratch reservations from admitted retained JSON bytes.</summary>
internal sealed class BoundedJsonBuffer(int maximumBytes, CancellationToken cancellationToken) : IBufferWriter<byte>
{
    /// <summary>Retains only bytes admitted within the output limit.</summary>
    private byte[] retained = new byte[Math.Min(maximumBytes, 256)];
    /// <summary>Provides a bounded contiguous reservation required by the native JSON writer.</summary>
    private byte[] scratch = [];
    /// <summary>Tracks committed output, independent of scratch reservation size.</summary>
    private int length;

    /// <summary>Copies only a successfully sealed admitted result.</summary>
    public byte[] ToArray() => retained.AsSpan(0, length).ToArray();
    /// <inheritdoc />
    public void Advance(int count)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (count < 0 || count > scratch.Length) throw new ArgumentOutOfRangeException(nameof(count));
        if (count > maximumBytes - length) throw new JsonOutputLimitException();
        var required = length + count;
        if (retained.Length < required)
            Array.Resize(ref retained, (int)Math.Min(maximumBytes, Math.Max(required, 2L * retained.Length)));
        scratch.AsSpan(0, count).CopyTo(retained.AsSpan(length));
        length = required;
    }
    /// <inheritdoc />
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        Reserve(sizeHint);
        return scratch;
    }
    /// <inheritdoc />
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        Reserve(sizeHint);
        return scratch;
    }
    /// <summary>Bounds reservations before allocation, including native worst-case UTF-16 JSON escaping.</summary>
    private void Reserve(int sizeHint)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        // Utf8JsonWriter reserves up to six bytes per UTF-16 code unit even when the
        // actual encoded token fits. Admission happens in Advance, before retention.
        var reservationLimit = checked(6 * maximumBytes + 4096);
        if (sizeHint > reservationLimit) throw new JsonOutputLimitException();
        var required = Math.Max(256, sizeHint);
        if (scratch.Length < required) scratch = new byte[required];
    }
}
