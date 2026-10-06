using Orbyss.Foundation.Collections.Core;

/// <summary>Exercises owned sequence values nested in another owned sequence.</summary>
/// <param name="Items">Nested ordered values.</param>
public sealed record NestedHolder(ValueSequence<ValueSequence<string>> Items);
