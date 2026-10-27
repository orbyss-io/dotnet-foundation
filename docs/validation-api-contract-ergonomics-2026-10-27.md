# API contract ergonomics and numeric schema correction

The change keeps operation metadata beside its actual HTTP handler and wire types, while retaining native
Foundation JSON admission, permission policies and Problem Details. It adds thin helpers to existing owners;
there is no dispatcher, assembly scan, new lifecycle or MinimalApis package.

## Review the delivered path

- `tests/dotnet/Orbyss.Foundation.Json.Admission.Probe/Operations/ChangeValue/Endpoint.cs` owns route/name,
  mapped `If-Match` binding, raw duplicate-header checks, bounded request reading and typed response writing.
  `ReadValue/Endpoint.cs` registers the same shared response. The feature only composes them.
- `JsonContractMetadata<T>.Request/Response` and named `JsonProfileRequirement` factories describe one
  immutable contract. `AddJsonContract` and the endpoint overloads consume that same definition. Equivalent
  repeated registrations are idempotent; conflicting known definitions reject before mutation. Metadata-only
  matching declarations receive the native reader/factory without overriding an intentional custom factory.
- `AddJsonEndpoint<T>` retains request-scoped identity/disposal and defers runtime-only constructor dependencies
  during cold metadata composition. Ordinary typed DI registrations remain appropriate elsewhere. A lifecycle
  initializer still uses the native CShells path and its actual ownership/disposal constraints.
- `RequirePermission` selects the existing canonical policy. Permission keys remain semantic feature contracts.
  `ProducesFoundationProblems` publishes actual failure definitions; it does not install storage/error filters.

## Reproduced defect and correct owner

With SDK 10.0.402 / ASP.NET OpenAPI 10.0.13, explicit class/property `JsonNumberHandling.Strict` rejects quoted
nullable integers at runtime, but the native schema still includes string for nullable int/long under the
default web `AllowReadingFromString` options. Setting global web options to Strict repairs that case but
incorrectly removes strings for explicitly permissive nullable properties, which runtime still accepts.
SDK 10.0.403 / ASP.NET 10.0.14 reproduces both outcomes. Nullable producer schema policy can therefore come
from global web options rather than the property's effective serializer policy. Consumer-specific global
options or per-DTO numeric-union rewriting cannot repair both contracts correctly.

The reusable owner is `JsonContractSchemaTransformer` in Json.AspNetCore. It uses each registered profile's
actual frozen `JsonTypeInfo`, with property > containing type > options precedence. The bounded numeric policy
supports built-in integer/floating/decimal families, nullable values, collections and explicitly declared
polymorphic derived metadata. Read and write flags are separate. It retains requiredness, descriptions,
ranges, independently owned patterns/unions and local references. Opaque custom converters retain an actual
native schema transformer owner. Conflicting numeric meanings for one shared CLR schema reject explicitly;
ordinary same-type strict request/response works. This correction is specifically numeric: unknown-member,
naming and custom converter shape need explicit DTO metadata/native owners and real export qualification. Recursive collection metadata traversal is finite.

The producer's default numeric-string regex also rejects native-valid leading plus/zero, decimal and exponent
forms. Only demonstrated producer patterns are removed; explicit pattern annotations remain owned constraints.
This publishes structural token kinds without pretending to encode every parser range/value rule as a schema.
Schema-valid input can still fail native parser/range, application validation or byte/depth admission.

The existing `x-foundation-json-contracts` bounds retain supported-interval semantics. The probe deliberately
uses active 8192-byte/depth-8 profiles with supported 16-MiB/depth-64 ceilings. No endpoint registration silently
chooses deployment budgets or equates JSON Schema character length with serialized UTF-8 bytes.

## Actual checks and retained output

`python tests/validate_json_admission.py` builds the actual probe and runs HTTP, four activation negatives and
the new `ergonomics` mode. It passed with native shell profiles/readers/results, numeric policy matrix,
malformed/duplicate/stale headers, shared contracts, conflicts, scoped disposal, cold DI and actual OpenAPI.
The probe writes its actual document under its project `artifacts/api-ergonomics/actual-contract.openapi.json`.
Existing `Web.HostedPages.Probe` also passed real anonymous-401, missing-permission-403, foreign-owner-404 and
permitted-200 controls after its actual caller adopted `RequirePermission`.

Local before/after files and original failures remain under ignored repository `artifacts/api-ergonomics/`:
`repro-10.0.13` and `repro-10.0.14` retain the strict/loose counterexamples and lexical matrix;
`json-admission-final.log` retains maintained validation; `permissions-http.log` retains permission HTTP proof.
The initial sandbox loopback/formatter-pipe failures were host access restrictions; approved local-process
reruns passed. No assertion or production behavior was weakened for those restrictions.

The existing Program Kit Roslyn syntax oracle checked the actual emitted operation plus every new Foundation
C# file: 49 files, 14 adverse syntax controls, zero violations. That establishes syntax/layout only; semantic
ownership and native execution are separate review obligations. Existing untouched Foundation fixture styles
were not broadly rewritten. Source inventories were refreshed with `scripts/refresh_feature_metadata.py`.

Exact-source solution CI, public package publication and Program Kit released-package cold export/generated
client qualification remain release-coordinator duties. Local HTTP producer and cold DI proof do not substitute
for those gates. The halted trial consumer was not modified or resumed.

## Application admission versus configuration

An application initializer such as `PolicyJsonAdmission` reads the configured native profile catalog, then
checks whether a product's configured collection/page/source shapes can fit that profile before accepting
requests. That is useful application activation admission when those capacities are product-owned. It does
not replace Microsoft Options binding/`IValidateOptions<T>`, nor belong in generic Core as a provider-specific
restriction. Preserve its actual shape/capacity guarantees when simplifying registration; do not remove it
merely because Foundation already validates profile syntax and supported intervals.
