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

## Descriptive operation contracts

An operation owns one immutable declaration used by registration and mapping:

```csharp
var supported = JsonProfileRequirement.StrictRequest(maximumBytes: 1024);
var contract = JsonContractMetadata<Request>.Request(supported);
services.AddJsonContract(contract);
services.AddJsonEndpoint<Endpoint>();
endpoints
    .MapPost(Endpoint.Route, Endpoint.Invoke)
    .WithJsonRequest(contract);
```

Keep the declaration on the wire contract or operation owner. `JsonContractMetadata<Response>.Response()`
selects the configured success-response profile. Named requirement factories preserve existing supported
defaults. Equivalent repeated typed/direction registrations are idempotent, allowing two operations to
register a shared response. Conflicting profile or capacity declarations fail before collection mutation.
Matching metadata-only registrations receive their native reader/factory; intentionally registered custom
implementations remain. Unknown metadata factories stay deferred; native activation rejects ambiguity.

`AddJsonEndpoint<T>()` is a scoped constructor factory for metadata-only composition that excludes runtime
effects. It adds no dispatch, scanning or lifecycle. Ordinary `AddScoped<IService, Service>()` remains
appropriate when its dependencies belong to composition. Mapped methods retain route/header/service binding
attributes, including optional headers and duplicate header cardinality, and use typed readers/results.

`x-foundation-json-contracts` minimum/maximum byte/depth fields describe **supported intervals**, not active
budgets. Actual `MaxBytes`/`MaxDepth` remain shell options. JSON Schema does not replace serialized UTF-8 byte
limits, parser ranges, application validation, authentication or ownership.

The native schema transformer reconciles built-in numeric token kinds with the registered profile's frozen
serializer metadata (property, containing type, then options precedence). Request reading and response writing
are distinct: `AllowReadingFromString` broadens input; `WriteAsString` changes output. Nullable numbers,
numeric collections and declared polymorphic derived types use the same policy. Known producer-generated
numeric-string patterns that reject native-valid plus/leading-zero/decimal/exponent variants are removed;
independently owned patterns, ranges, requiredness and unions remain. This is not a numeric parser encoded as
JSON Schema, and schema validity alone does not promise runtime acceptance.

Custom numeric converters retain their native explicit schema owner; Foundation does not infer converter
semantics. Incompatible numeric meanings for one shared CLR schema fail cold composition with a diagnostic:
align policies or use distinct wire types. Ordinary strict request/response reuse remains supported.
Referenced numeric constraints are narrowed locally or rejected if they cannot represent the declared shape;
shared targets are never globally rewritten. Other profile-specific unknown-member/naming/converter/schema differences still
require their native schema owner and actual export qualification.
