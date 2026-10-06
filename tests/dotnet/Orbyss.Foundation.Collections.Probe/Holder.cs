using Orbyss.Foundation.Collections.Core;

/// <summary>Exercises containing-record equality and the existing JSON array shape.</summary>
/// <param name="Items">Ordered owned contract values.</param>
public sealed record Holder(ValueSequence<string> Items);
