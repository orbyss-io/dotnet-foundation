namespace Orbyss.Foundation.Json.Admission.Probe.Contracts;

/// <summary>Proves numeric metadata traversal terminates on a serializer-supported recursive collection.</summary>
internal sealed class RecursiveCollection : List<RecursiveCollection>;
