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
