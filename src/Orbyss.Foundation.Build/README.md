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
initializers are statically evaluated primitive constants, string.Empty and string collection expressions; unsupported
expressions fail packing and need a separately qualified owning exporter. Secrets omit defaults.

Owners review constraints, precedence, binding and reload semantics against their validators;
the full source inventory makes changes stale until reviewed. `complete` covers the named
type only, not all configuration of a package or host. Source hashes prove review freshness,
not automatic derivation of arbitrary semantic rules. Json's first real contract covers
JsonProfileSettings code construction; Json.AspNetCore binding, host/CShells/Nuplane,
authentication and other options are explicitly uncovered. The package receiver must bind
the companion to the exact selected nupkg ID/version/hash. This candidate is not published.

Build 0.2.0 is a source candidate; published 0.1.0 remains immutable. Metadata is
created after Build and binds the actual final assembly hash. Packing revalidates
that compiled metadata; `--no-build` cannot regenerate provenance for changed source
or declarations. Receivers verify the bound assembly inside the exact selected package.

The build task uses the repository's centrally pinned Microsoft.CodeAnalysis.CSharp
dependency and bundles its two parser assemblies and notices under build tooling.
PrivateAssets and suppressed packing dependencies keep them out of runtime consumers.

Review repair: constant defaults use Roslyn semantic constant evaluation with the actual
compiler language version and resolved reference assemblies. No code runs. Conditional
directives (#if/#elif/#else/#endif/#define/#undef) are rejected throughout reviewed source
instead of guessing DefineConstants; this limitation is exercised with a real FEATURE build.
Metadata is emitted after Build from TargetPath, and pack checks NuGet's actual
_BuildOutputInPackage FinalOutputPath against that final assembly and the compiled receipt.
Mutating bin alone before --no-build packing rejects without replacing prior artifacts.

Direct metadata target invocation resolves Compile and CopyFilesToOutputDirectory;
SkipCompilerExecution and DesignTimeBuild cannot create or validate publisher provenance.
Admission caps: 1 MiB declaration, 512 sources/references, 1 MiB per source and 16 MiB
source total, 64 MiB per reference and 256 MiB reference total, 32 contracts,
256 settings/contract, 128 semantic strings, 4 Ki-character declaration text,
16 KiB constraints, 16 Ki-character string defaults, 256 default array items,
and 2 MiB emitted payload. Violations preserve existing metadata and packed output.

Encoding admission is incremental: each setting is encoded before retaining another,
through one fixed non-growing 2 MiB IBufferWriter. Expanded defaults hold typed JsonValue
references rather than pre-serialized JsonElements. Final indented payload and constraints
also use that bounded encoder; oversized output fails during encoding, never after building
an unbounded encoded string. Final/packed assemblies are streamed through SHA256 after a
256 MiB file admission check. Installed tests cover one >4 MiB expanded array and 253 arrays
whose admitted source/declaration would expand past 1 GiB, preserving existing output.
