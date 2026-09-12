# Issue 2: typed JSON profiles and versioned canonicalization

Issue: https://github.com/orbyss-io/dotnet-foundation/issues/2

Status: implemented locally as the second issue in the shared 0.2.0 bump. See [validation evidence](../validation-0.2.0.md) and the [approved canonicalizer implementation decision](../json-canonicalization-decision.md). Consumer migration and publication have not been performed.

## Outcome

Provide a small System.Text.Json-based library for typed payloads, deliberate immutable profiles, controlled extensions, and explicitly versioned canonicalization. Optional integration binds shell settings and maps HTTP errors without introducing public endpoints or behavior in Host.

## Agreed decisions

- Keep RM-01's legacy adapter and product normalization product-owned. Foundation supplies reusable primitives and regression fixtures, not product-specific ordering or identity logic.
- Strict request profiles have fixed admission guarantees. Settings select profiles and bounded limits; code registers additional compatibility profiles. Tolerant response readers are separate.
- Converters and resolvers are registered by code under allowlisted identifiers. Configuration cannot load arbitrary CLR types or code.
- Support typed DTOs and explicit source-generated metadata. Keep dynamic JSON only where schemas or token-level requirements are actually dynamic.
- Migrate one representative Foundation reader first, with focused probes. Broad conversion of OAuth/provider readers is a separate reviewed effort.
- A maintained third-party canonicalizer is acceptable only after license, maintenance, correctness, packaging, and cross-language vector assessment. If no candidate qualifies, return with evidence and a separate implementation proposal.

## Current implementation

The repository pins SDK 10.0.202 and targets net10.0. Its installed reference pack exposes duplicate-property handling options; implementation must verify these with a focused compile probe rather than relying on online prerelease assumptions. No common JSON profile service or source-generation contexts were found.

`FilePublicDocumentCatalog` already deserializes a typed manifest and is a suitable representative migration. Preserve its strict public-resource admission. Provider readers such as Keycloak contain legitimately dynamic contracts and must not inherit strict server-request defaults indiscriminately.

## Agreed package boundary

- `Orbyss.Foundation.Json`: typed profiles, bounded operations, errors, controlled registration, and explicit canonicalization contracts/implementation selected after dependency review. No ASP.NET or CShells dependency.
- `Orbyss.Foundation.Json.AspNetCore`: optional shell configuration and HTTP integration.

Both projects belong directly under `src/`. Canonicalization must remain deliberately selected; ordinary serialization must not silently canonicalize data.

## Implementation sequence

1. Compile a small pinned-runtime capability probe covering duplicate handling, immutable options, nullable/required behavior, and source generation. Define the supported profile matrix, error contract, and byte/depth bounds.
2. Implement immutable profile construction and lookup, typed serialize/deserialize operations, explicit `JsonTypeInfo<T>` overloads, and code-only extension registration. Reject duplicate IDs, incompatible converters, resolver ambiguity, and invalid configuration during composition.
3. Add optional shell-local profile catalogs and explicit HTTP binding. Use stable safe error codes and existing Problem Details conventions. Bound bytes while reading; do not rely on declared Content-Length or expose payloads in diagnostics.
4. Assess concrete RFC 8785 implementations and record the selection decision. Define algorithm/version identifiers, deterministic UTF-8 output, supported number domain, string validity, object-key ordering, and unchanged array order. Never claim RM-01 raw-number behavior is RFC 8785.
5. Provide reusable raw-number-preserving primitives only where necessary. Obtain representative legacy byte/digest fixtures and demonstrate adapter composition without changing the consumer. Product answer sorting, language order, explicit nulls, and journey/revision identities remain product rules.
6. Migrate the representative manifest reader and provide before/after typed examples, shell-settings examples, and converter/source-generation examples. Add probes and validator wiring.

## Regression evidence

- Known DTO round trips, missing versus explicit null, required/nullable members and collection elements, unknown and duplicate members including escaped-equivalent names, case sensitivity, and discriminated variants.
- Invalid UTF-8, lone surrogates, depth/size limits, numeric strings, overflow and raw numeric lexemes, deterministic errors, and non-disclosure of sensitive payloads.
- Converter/resolver conflicts, no arbitrary type loading, shell/profile isolation, immutable behavior, and source-generated/reflection parity.
- Cross-language golden vectors for object order, unchanged arrays, exact strings and UTF-8 bytes, algorithm version selection, invalid inputs, and digests.
- Separate legacy fixtures for exact raw numbers, product answer sorting, retained language order, explicit nulls, and journey/revision references. Missing authoritative legacy fixtures is a reported evidence gap, not a claimed compatibility success.

## Completion and limits

Run locked restore, Release build, focused and existing validators, and package validation. Update solution, lockfiles, documentation, and all package-family assertions for reviewed additions. No global replacement of every JSON reader, new public utility endpoint, NativeAOT certification, consumer migration, tagging, or publication is implied.

## Canonicalizer assessment recorded during planning

No production dependency is selected. Evaluate `MackySoft.Json.Canonicalization` as one candidate, alongside the Cyberphone reference implementation. Produce a reviewable record of the exact package/version/source commit, license/notices, dependencies, published-artifact provenance, maintenance, pinned-runtime suitability, and conformance results.

- [Cyberphone C# source](https://github.com/cyberphone/json-canonicalization/tree/master/dotnet) is a useful reference, but [issue 34](https://github.com/cyberphone/json-canonicalization/issues/34) reports a C# subnormal-number formatting defect. This report was not independently reproduced during planning.
- [jsoncanonicalizer 1.0.0 on NuGet](https://www.nuget.org/packages/jsoncanonicalizer/1.0.0) does not establish its relationship to the reference source through repository/license links. Verify the actual artifact instead of assuming provenance.
- [MackySoft's source repository](https://github.com/mackysoft/dotnet-foundations) is another candidate; its young release history and source/package correspondence need assessment.
- Use the commit-pinned [reference test corpus](https://github.com/cyberphone/json-canonicalization/tree/master/testdata), [RFC 8785 Appendix B](https://www.rfc-editor.org/rfc/rfc8785.html#appendix-B), and additional cases including positive/negative `1e-320`, top-level scalars, duplicate keys, invalid Unicode, UTF-16 key ordering, and array preservation.
