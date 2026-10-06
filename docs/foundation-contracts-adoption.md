# Foundation contracts adoption

This candidate adds six packages: Authentication.Core, Web.ProblemDetails.Core,
Collections.Core, Execution.Core, Execution and PostgreSql. Existing Abstractions packages
retain their names. Contract packages contain values/interfaces and have no shell feature;
Execution also offers an optional registration feature. PostgreSql registration belongs to
the application's provider feature. The Host remains application-neutral.

## Identity and problems

Use `IValidatedAccountIdentityReader.TryRead(HttpContext.User, out account)` in HTTP adaptation.
`ValidatedAccountIdentity` retains the exact validated issuer and subject. The default reader
requires exactly one total authenticated identity and one nonempty reserved issuer/subject pair.
Use `AuthenticationClaimTypes` and `AuthenticationErrorCodes` for exported identifiers.
Application policy still checks resource ownership and parent membership.

BFF cookie tickets now bind the owning shell. Previously unbound sessions require reauthentication.
This does not change business identities or stored revisions. BFF user, antiforgery and signed-out
responses use the configured success profile; their property sets remain compatible. Generated
resolvers must include `BffUserResponse`, both derived responses, `BffAntiforgeryResponse` and
`BffSignedOutResponse`, plus their closed member types.

Applications map ordinary denials through `IProblemMapper<TFailure>` and return
`FoundationProblemResults.Problem(definition)`. `ProblemDefinition` owns bounded immutable text and
field diagnostics. Register scoped `IProblemDetailsEnricher` contributions for request-specific
representation. `IAuthenticationErrorWriter` remains the authentication adapter.

The canonical correlation extension is `correlationId`; `traceId` remains a compatibility alias.
Problem writing uses one separately bounded closed encoder. Its byte/depth/preset settings come
from the problem-response profile when Json.AspNetCore is selected. Application converters/resolvers
cannot replace the internal failure encoder. A 512-byte/depth3 minimum admits a fixed nonrecursive
safe fallback. Optional global exception-feature activation is independent of basic auth/JSON writing.
The public problem-result interface describes shape; it does not prove bounded admission. Typed
endpoint guards accept the library's sealed result or a native failure result through the common writer.

Native `IExceptionHandler` contributions are singleton and ordered. Resolve scoped dependencies
from the request provider. Programming defects retain safe 500 handling; arbitrary argument exceptions
do not become client 400. Cancellation does not fabricate a completed error response, and a started
response cannot be replaced. The Host and provider retain classified, redacted native diagnostics.

The managed Host uses native status-code pages for uncommitted empty routing failures.
Unowned 404/405 responses use the common bounded policy; requests selected into a shell
retain its scoped enrichment and configured problem budget, even without the optional
global exception feature. Existing bodies, cancellation, started output and native `Allow`
headers retain their meaning.

The new Host owns six shared assembly identities: `CShells.Abstractions`,
`CShells.AspNetCore.Abstractions`, `Orbyss.Foundation.Web.ProblemDetails`,
`Orbyss.Foundation.Web.ProblemDetails.Core`, `Orbyss.Foundation.Json` and
`Orbyss.Foundation.Collections.Core`. Preserve those shared-assembly settings when composing
deployment configuration. Keep all exact archives in qualification evidence; omit these six
from loader roots only after their selected versions and net10 DLL hashes match the retained
Host payload. An old two-name shared-assembly override is not qualified for this new Host.
Historical profiles continue to select their historical Host.

Release images copy the portable Host payload produced by no-build publish and qualification,
including effective JSON configuration and native runtime assets. They do not rebuild shared
assemblies after package qualification. The tagged workflow verifies these bytes against the
actual packaged shell/PostgreSQL run before package and image publication.

## Typed JSON and transport

Register operation-owned wire types and requirements in the API feature:

```csharp
services.AddJsonRequestContract<RenameRequest>(
    new(JsonProfileKeys.StrictRequest), new(JsonProfileKeys.StrictRequest));
services.AddJsonResponseContract<RenameResponse>(
    new(JsonProfileKeys.SuccessResponse), new(JsonProfileKeys.TolerantResponse));
```

Inject `IJsonRequestReader<RenameRequest>` and `IJsonResponseFactory<RenameResponse>` into the
endpoint instance. Read through the reader and return `responses.Create(value)`; map denials through
the common problem result. Declare `.WithJsonRequest<RenameRequest>()` and
`.WithJsonResponse<RenameResponse>()` on the mapped operation. Native DTO body binding, direct body
reads/writes and successful results bypassing the declared adapter fail admission. OpenAPI uses
the same contract metadata; it does not need storage initialization.

Configure values once under `Foundation:Json`: strict request 2 MiB, success/page 1 MiB,
problem 64 KiB, depth 32, page size 100 and maximum requested page size 500 are the initial defaults.
Use `FoundationJsonOptions.Paging.Admit(size)` for count admission. Byte and count bounds both apply.
Generated metadata must cover every registered root and member type. Actual shell activation rejects
unsupported presets, capacities, missing metadata and duplicate registrations.

Client input failures expose `JsonProfileException`; invalid/oversized server output exposes
`JsonResponseContractException`, including `JsonOutputLimitException`. Stable classifications live
in `JsonFailureCodes`. Output is admitted before success commitment. Retained encoded bytes stay
within MaxBytes; native contiguous scratch reservations have a separate finite bound of
`6 * MaxBytes + 4096`. Native string/key encoding pre-counts scalar bytes. Trusted converters must
delegate string serialization through the configured serializer and keep their own allocations bounded.
Application construction and storage reads still need independent bounds.

Process limits live under `Foundation:Transport` in Host configuration and are applied before startup.
Body and header ceilings are independent from decoded JSON budgets and reverse-proxy policy.
Chunked framing can consume transport bytes in addition to decoded JSON bytes. Shell configuration
does not change process-global Kestrel limits. Qualify supported framing/proxy settings together.

Build0.2.0 provides a separate publisher-owned settings metadata companion. Its first owner
declaration describes the code-constructed `JsonProfileSettings` type, derived from source defaults
and bound to the final packed assembly. It does not claim coverage of shell `Foundation:Json`,
Host transport, authentication, PostgreSQL, CShells or Nuplane settings. Those effective deployment
settings still require their owner-specific validation and acceptance tests. Metadata export never
executes publisher initialization or starts storage.

## Independent persistence units and deadlines

Keep EF entities, mappings, migrations and configuration in the provider assembly. Its context derives
from `FoundationPostgreSqlDbContext` and retains the supplied `DbContextOptions<TContext>`;
registration owns native provider configuration. Register from the provider feature:

```csharp
services.AddFoundationPostgreSql<NotesDbContext>(settings, "primary",
    options => new NotesDbContext(options));
```

Bind `Foundation:PostgreSql:Policies:primary` with a deployment connection string and operation,
connection, command, lock and cancellation-acknowledgement durations. Registration validates an
immutable snapshot and shares one stable datasource per shell generation/policy. Context pooling
is disabled; native connection pooling remains independent.

```csharp
await using var unit = await units.BeginUnitAsync(callerCancellation, outerDeadline);
await using var db = await unit.Factory.CreateDbContextAsync(unit.Deadline.Token);
var rows = await db.Notes.Take(pageSize).ToListAsync(unit.Deadline.Token);
```

Every factory unit owns an actual tracked shell scope, including initialization/background cleanup.
Root/plain DI factory creation fails. Closing the owner stops new admission; existing contexts retain
the tracked scope and datasource until their disposal. Await every context disposal. Concurrent and
reentrant disposal share outcomes. A mutation and receipt reconciliation use distinct units and the
same outer monotonic deadline. `IExecutionDeadlineFactory` supports replacement `TimeProvider` for
deterministic time tests; stages/retries cannot reset the outer budget.

Native command cancellation remains active through streamed reader close. Driver acknowledgement
has its own finite allowance. Cancellation or a missing acknowledgement does not prove rollback or
commit failure. Use `PostgreSqlFailureInfo` for safe SQLSTATE/constraint extraction; the application
owns constraint mappings, schema expectations, transactions, replay and reconciliation.
Pass `unit.Deadline.Token` explicitly to transaction begin, commit and rollback calls as well as
queries and mutations. Native transaction dispatch has no automatic command-stage cap. Async EF
command dispatch receives the capped stage token; sync completion checks elapsed time too. A canceled
sync command may already have committed, so reconcile through a fresh unit instead of assuming rollback.

## Canonical identity and delivery migration

`ValueSequence<T>` owns a shallow defensive copy with ordered value equality. It does not deep-freeze
mutable elements, and runtime GetHashCode is not a persisted digest. Select `ValueSequenceJsonExtension`
for array adaptation; generated closed metadata is supported.

`CanonicalUtf8Writer` provides bounded strict UTF-8/minimal escaping and incremental SHA-256 in
caller-owned schema order. It does not choose normalization, sorting or an algorithm version.
Overflow, malformed Unicode or destination failure prevents digest sealing. Preserve historical
canonical-v1 bytes/hashes and receipt identities. Delivery paging and canonical identity are separate
compatibility decisions.

De Zaaglijst remains read-only in this implementation. Its project split, Foundation adoption,
storage repairs, immutable result leases, authenticated page manifests and incremental browser
verification require a separate consumer specification after Foundation publication. The independent
packaged contract fixture and Notes specimen supply pre-publication acceptance; they do not establish
that deferred consumer rollout.
