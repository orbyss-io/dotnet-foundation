# Orbyss.Foundation.Json

Typed System.Text.Json operations with immutable named profiles. No ASP.NET or CShells dependency.

```csharp
var profile = new JsonProfile(new JsonProfileSettings());
var request = profile.Deserialize<MyRequest>(utf8);
var bytes = profile.Serialize(request);
var streamed = await profile.ReadAsync<MyRequest>(stream, cancellationToken);
```

Use records with required constructor parameters or required properties. Explicitly nullable required
members distinguish missing from null. Strict requests are camel-case, case-sensitive, reject unknown
and duplicate decoded members, reject numeric strings, honor nullable annotations and required
constructors, and default to 1 MiB / depth 32. Limits may be lowered or raised up to 16 MiB / depth 64.
The tolerant-response preset permits unknown members and case-insensitive matching, but still rejects
duplicate logical names, coercion, and malformed Unicode. A null root is rejected.

.NET nullable annotations do not validate collection-element nullability or arbitrary product invariants.
Express element requirements with a code-owned converter/validator; this library does not pretend those
annotations provide a complete schema. Discriminated variants use System.Text.Json polymorphism metadata.
Registered converters are trusted code and must preserve their declared contract and remain stateless.

Register IJsonProfileExtension instances by code, then select their IDs through settings. Unknown,
duplicate IDs and multiple selected resolvers fail composition. Converter overlap fails when root or
nested metadata is resolved; resolve known TypeInfo<T>() contracts during activation for early checks.
No configuration value is treated as a CLR type. Explicit metadata must come from profile.TypeInfo<T>();
install a source-generated context as an extension Resolver to get the same frozen options. A profile
does not fall back to reflection when a selected resolver lacks a contract.

JsonProfileException exposes deterministic safe codes. Contract/syntax diagnostics omit payloads.
Stream reads enforce the actual byte count regardless of declared content length and honor cancellation.
Serialization bounds are checked after encoding; output objects must already be reasonably bounded.

## Canonicalization and compatibility

JsonCanonicalizer.Canonicalize accepts the explicit algorithm
`rfc8785-reject-negative-zero-v1`: UTF-16 ordinal object keys, unchanged arrays, exact strings without
Unicode normalization, finite binary64 number interpretation and ECMAScript decimal placement, UTF-8
bytes. Negative zero is rejected under RFC erratum 7920. Do not use it for arbitrary-precision identities.

RawJsonNumber preserves a numeric token lexeme through typed serialization. It is a compatibility
primitive, not RFC 8785. Product normalization (including answer ordering), authorization and deduplication
remain product-owned. RM-01 captured numeric `raw`/`normalized` values are strings; preserve them exactly.
Do not replace legacy readers or wire algorithms without old byte/digest fixtures and version review.

The dependency assessment and approved implementation exception are in
[the repository decision record](https://github.com/orbyss-io/dotnet-foundation/blob/main/docs/json-canonicalization-decision.md).
Run `python tests/validate_json.py` after the Release build; Node provides independent V8 vectors.
