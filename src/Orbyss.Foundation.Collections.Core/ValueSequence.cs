using System.Collections;

namespace Orbyss.Foundation.Collections.Core;

/// <summary>Owns a shallow ordered snapshot with value equality. Mutable elements are not deep frozen.</summary>
public sealed class ValueSequence<T> : IReadOnlyList<T>, IEquatable<ValueSequence<T>>
{
    /// <summary>Retains the privately owned shallow snapshot.</summary>
    private readonly T[] items;

    /// <summary>Gets the shared empty sequence.</summary>
    public static ValueSequence<T> Empty { get; } = new([]);

    /// <summary>Copies the source and rejects null items. Elements retain their own equality semantics.</summary>
    public ValueSequence(IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        items = source.ToArray();
        if (items.Any(item => item is null)) throw new ArgumentException("Sequence items cannot be null.", nameof(source));
    }

    /// <inheritdoc />
    public int Count => items.Length;
    /// <inheritdoc />
    public T this[int index] => items[index];
    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();
    /// <inheritdoc />
    public bool Equals(ValueSequence<T>? other) => other is not null && items.SequenceEqual(other.items);
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ValueSequence<T> other && Equals(other);
    /// <summary>Hashes ordered runtime values. This is not a persisted or cryptographic digest.</summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in items) hash.Add(item);
        return hash.ToHashCode();
    }
    /// <summary>Enumerates values without exposing the owned array.</summary>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
