# Orbyss.Foundation.Build

Build-only, publisher-owned descriptor production. Reference with `PrivateAssets="all"`.
Set `FoundationFeatureIdentity`, `PackageId`, `FoundationFeatureDependencies`,
`FoundationRuntimeDependencies`, `FoundationFeatureRoutes` and optional
`FoundationComposeForOpenApi`/`FoundationFeatureDormant`. Packing emits canonical
`orbyss-foundation/feature.json`, described by the packaged schema. No runtime
assembly or dependency is contributed to the consumer.

Publishers can supply `FoundationFeatureDescriptorSource` for a schema 2 descriptor
with multiple feature identities. Its `sourceSha256` inventory binds reviewed
source bytes; pack fails when a compiled source changes or a declared feature is
missing. Review the owning descriptor whenever implementation changes.
Both supplied and generated metadata are checked before replacing the descriptor
or packing the package. Malformed inventories, duplicate identities/properties,
relative routes, mismatched configuration pairs and invalid dependency/source
bindings fail with a publisher diagnostic. Historical valid outputs remain intact.
`hostProvidedDependencies` records runtime package minimum versions from the
resolved assets file. Consumers retain their qualified exact locks when a newer
resolved version satisfies that minimum. These facts are distinct from
application dependency selection and grant no compatibility approval.

Routes may name a configuration path and suffixes for a configurable prefix.
Dynamic routes require host integration acceptance. Platform metadata can exclude
host-only adapters from offline endpoint composition; application features remain
subject to complete contract feature coverage.

## Settings companion (source candidate)

Set `FoundationSettingsMetadataSource` to an owner-reviewed declaration matching
`schemas/settings-source.schema.json`. Packing produces `orbyss-foundation/settings.json`
with actual package ID/version and normalized compilation source hashes. Repository
publishers use `settings.source.json`. The companion does not change FeatureDescriptors.
Multiple settings scopes can belong to a package with zero, one or multiple features.

Settings declarations identify a namespace-qualified options class and each public property.
Types and supported literal defaults are read from Roslyn syntax, never supplied by hand.
No assembly, constructor, initializer, service registration, storage or identity provider is
executed. Supported classes are non-partial, non-inherited and constructor-free with auto
properties of string, bool, int/long, floating/decimal types or string arrays. Supported
initializers are literals, string.Empty and literal string collection expressions; unsupported
expressions fail packing and need a separately qualified owning exporter. Secrets omit defaults.

Owners review constraints, precedence, binding and reload semantics against their validators;
the full source inventory makes changes stale until reviewed. `complete` covers the named
type only, not all configuration of a package or host. Source hashes prove review freshness,
not automatic derivation of arbitrary semantic rules. Json's first real contract covers
JsonProfileSettings code construction; Json.AspNetCore binding, host/CShells/Nuplane,
authentication and other options are explicitly uncovered. The package receiver must bind
the companion to the exact selected nupkg ID/version/hash. This candidate is not published.

Build 0.2.0 is a source candidate; published 0.1.0 remains immutable. Metadata is
created after compilation and binds the actual assembly hash. Packing revalidates
that compiled metadata; `--no-build` cannot regenerate provenance for changed source
or declarations. Receivers verify the bound assembly inside the exact selected package.

The build task uses the repository's centrally pinned Microsoft.CodeAnalysis.CSharp
dependency and bundles its two parser assemblies and notices under build tooling.
PrivateAssets and suppressed packing dependencies keep them out of runtime consumers.
