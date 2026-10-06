using System.Buffers;

namespace Orbyss.Foundation.Build;

/// <summary>Provides exactly one 2 MiB allocation shared by all output encoding passes.</summary>
internal sealed class BoundedJsonBuffer : IBufferWriter<byte>
{
    /// <summary>Stores the finite encoding space.</summary>
    private readonly byte[] bytes = new byte[2_097_152];
    /// <summary>Stores the current admission ceiling.</summary>
    private int limit;
    /// <summary>Gets the bytes committed by the encoder.</summary>
    public int Length { get; private set; }
    /// <summary>Restarts encoding within the remaining global budget.</summary>
    public void Reset(int maximum) { Require(maximum is > 0 and <= 2_097_152, "Settings encoded output exceeds 2 MiB limit."); limit = maximum; Length = 0; }
    /// <summary>Accounts encoder output before it can exceed the fixed memory.</summary>
    public void Advance(int count) { Require(count >= 0 && count <= limit - Length, "Settings encoded output exceeds 2 MiB limit."); Length += count; }
    /// <summary>Returns only the available bounded memory.</summary>
    public Memory<byte> GetMemory(int sizeHint = 0) { Check(sizeHint); return bytes.AsMemory(Length, limit - Length); }
    /// <summary>Returns only the available bounded span.</summary>
    public Span<byte> GetSpan(int sizeHint = 0) { Check(sizeHint); return bytes.AsSpan(Length, limit - Length); }
    /// <summary>Copies the admitted final bytes plus one newline, bounded by the same limit.</summary>
    public byte[] GetBytes(int length) { var result = new byte[length + 1]; bytes.AsSpan(0, length).CopyTo(result); result[length] = (byte)'\n'; return result; }
    /// <summary>Rejects a reservation before allocating any additional memory.</summary>
    private void Check(int sizeHint) => Require(Math.Max(1, sizeHint) <= limit - Length, "Settings encoded output exceeds 2 MiB limit (fixed buffer; no growth).");
    /// <summary>Rejects admission without reserving additional memory.</summary>
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
