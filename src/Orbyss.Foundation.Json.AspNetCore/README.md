# Orbyss.Foundation.Json.AspNetCore

Select the `Orbyss.Foundation.Json.AspNetCore` shell feature. It registers a shell-local JsonProfileCatalog
and maps client JsonProfileException to the shared Problem Details envelope (413 for byte limits;
400 for malformed/invalid input). Invalid/oversized server output is a safe 500 before commitment.
The canonical correlation key is `correlationId`; `traceId` remains its compatibility alias. The optional
global exception feature is independent of basic JSON/problem writing. It creates no public endpoints.

Configure the shell's `Configuration:Foundation:Json:Profiles`:

```json
{
  "strict-request": { "Preset": "strict-request", "MaxBytes": 2097152, "MaxDepth": 32 },
  "success-response": { "Preset": "tolerant-response", "MaxBytes": 1048576, "MaxDepth": 32 },
  "problem-response": { "Preset": "tolerant-response", "MaxBytes": 65536, "MaxDepth": 8 }
}
```

Register `AddJsonRequestContract<Request>(key, requirement)` and
`AddJsonResponseContract<Response>(key, requirement)` from an operation's activation adapter. Inject
`IJsonRequestReader<Request>` and `IJsonResponseFactory<Response>` into its instance endpoint, and add
`WithJsonRequest<Request>()` / `WithJsonResponse<Response>()` to the mapping. Each wire type/direction has
exactly one registered contract per shell. Actual activation validates supported intervals and serializer
metadata without resolving application/storage initialization. Common metadata also appears in OpenAPI
as `x-foundation-json-contracts` through the native operation transformer.

Declared request endpoints use the registered reader: native DTO body binding and direct Stream/BodyReader
parsing are rejected as server contract bypasses. The reader enforces actual decoded JSON bytes and
cancellation while preserving the independent native transport ceiling, which also covers HTTP framing.
Declared response endpoints reject unadmitted
results and early Stream/BodyWriter/StartAsync/SendFile writes. Typed success serialization completes
before the body gate opens, so overflow cannot become a truncated 200. Return shared admitted problems
for ordinary failures. Started/canceled transport failures retain native semantics.

The profile values above and `Paging:DefaultSize` / `Paging:MaximumSize` (defaults 100/500) are typed
deployment configuration. Count and byte limits both apply; applications own section/cursor/revision
semantics and must split items/sections that do not fit. These defaults are inputs to Notes qualification;
consumer geometry migration/largest-item qualification belongs to its later specification.

Code registers IJsonProfileExtension through DI; settings select allowlisted IDs only. Changes require
shell recreation. Requirements prevent silent reductions in supported capacity/strictness. Security and
authorization remain separate features. Process-wide Kestrel limits belong to Host `Foundation:Transport`
and reverse-proxy limits to deployment; neither is silently rewritten by a tenant.
