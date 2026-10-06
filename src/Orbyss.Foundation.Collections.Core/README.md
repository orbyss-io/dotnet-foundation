# Ordered collection values

`ValueSequence<T>` owns a defensive shallow copy, rejects null sources/items and compares ordered values. Empty sequences compare equal. Containing records retain structural equality. Mutable elements keep their own equality and can still mutate; this collection does not deep-freeze them. `GetHashCode` is a process/runtime value hash, never a retained digest.

Use BCL immutable collections when their ownership and equality semantics meet the model's requirements. `ImmutableArray<T>` has backing-array identity equality, which is insufficient for these containing-record contracts. This Core has no JSON, DI, shell or provider dependencies. JSON integration belongs to `Orbyss.Foundation.Json`.
