using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Orbyss.Foundation.Json;

/// <summary>Writes caller-owned schema order with minimal JSON escaping and incremental SHA-256.</summary>
/// <remarks>Does not normalize/sort or implement RFC 8785. The caller owns the destination and publishes only after completion.</remarks>
public sealed class CanonicalUtf8Writer : IDisposable
{
    /// <summary>Rejects malformed Unicode instead of replacing its bytes.</summary>
    private static readonly UTF8Encoding Utf8 = new(false, true);
    /// <summary>References caller-owned output, or no output in hashing mode.</summary>
    private readonly Stream? destination;
    /// <summary>Caps total admitted representation bytes.</summary>
    private readonly long maximumBytes;
    /// <summary>Owns only native incremental digest state.</summary>
    private readonly IncrementalHash hash;
    /// <summary>Prevents writing or resealing after success.</summary>
    private bool completed;
    /// <summary>Prevents publishing a digest after a partial failed write.</summary>
    private bool failed;
    /// <summary>Tracks disposal of owned digest state.</summary>
    private bool disposed;
    /// <summary>Gets the admitted encoded byte count.</summary>
    public long ByteCount { get; private set; }
    /// <summary>Creates a bounded writer. A null destination hashes/counts without retaining aggregate output.</summary>
    public CanonicalUtf8Writer(long maximumBytes, Stream? destination = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        if (destination is not null && !destination.CanWrite) throw new ArgumentException("Destination must be writable.", nameof(destination));
        this.maximumBytes = maximumBytes;
        this.destination = destination;
        hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    }
    /// <summary>Encodes an owned syntax fragment using strict Unicode and fixed-size scratch space.</summary>
    public void Raw(ReadOnlySpan<char> value)
    {
        RequireActive();
        Admit(value.Length);
        try { Admit(Utf8.GetByteCount(value)); }
        catch (ArgumentException) { failed = true; throw; }
        Span<byte> scratch = stackalloc byte[4096];
        while (!value.IsEmpty)
        {
            var take = Math.Min(value.Length, 1024);
            if (take < value.Length && char.IsHighSurrogate(value[take - 1])) take--;
            var count = Utf8.GetBytes(value[..take], scratch);
            Append(scratch[..count]);
            value = value[take..];
        }
    }
    /// <summary>Copies validated UTF-8 without decoding or retaining another aggregate buffer.</summary>
    public void RawUtf8(ReadOnlySpan<byte> value)
    {
        RequireActive();
        Admit(value.Length);
        try { _ = Utf8.GetCharCount(value); }
        catch (ArgumentException) { failed = true; throw; }
        Append(value);
    }
    /// <summary>Encodes literal Unicode, short control escapes and lowercase remaining control escapes; null stays null.</summary>
    public void String(string? value)
    {
        RequireActive();
        if (value is null) { Raw("null"); return; }
        Admit(value.Length);
        try { _ = Utf8.GetByteCount(value); }
        catch (ArgumentException) { failed = true; throw; }
        Raw("\"");
        var start = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character >= 32 && character is not ('"' or '\\')) continue;
            Raw(value.AsSpan(start, index - start));
            Raw(character switch
            {
                '"' => "\\\"", '\\' => "\\\\", '\b' => "\\b", '\t' => "\\t", '\n' => "\\n", '\f' => "\\f", '\r' => "\\r",
                _ => "\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture)
            });
            start = index + 1;
        }
        Raw(value.AsSpan(start));
        Raw("\"");
    }
    /// <summary>Seals the successful byte stream and returns its lowercase SHA-256. Further writes fail.</summary>
    public string CompleteSha256()
    {
        RequireActive();
        completed = true;
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        hash.Dispose();
    }
    /// <summary>Rejects operations after success, failure or disposal.</summary>
    private void RequireActive()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (completed || failed) throw new InvalidOperationException("Canonical output is no longer writable.");
    }
    /// <summary>Rejects overflow before retaining additional output.</summary>
    private void Admit(int length)
    {
        if (length > maximumBytes - ByteCount) { failed = true; throw new JsonOutputLimitException(); }
    }
    /// <summary>Writes admitted bytes and hashes exactly the same order.</summary>
    private void Append(ReadOnlySpan<byte> bytes)
    {
        try
        {
            destination?.Write(bytes);
            hash.AppendData(bytes);
            ByteCount += bytes.Length;
        }
        catch { failed = true; throw; }
    }
}
