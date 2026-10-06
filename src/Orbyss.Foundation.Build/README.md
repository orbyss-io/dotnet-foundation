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

## Settings metadata

Set `FoundationSettingsMetadataSource` to an owner-reviewed declaration matching
`schemas/settings-source.schema.json` or `schemas/settings-source-v2.schema.json`.
Packing produces `orbyss-foundation/settings.json`
with actual package ID/version and normalized compilation source hashes. Repository
publishers use `settings.source.json`. The companion does not change FeatureDescriptors.
Multiple settings scopes can belong to a package with zero, one or multiple features.

Settings declarations identify a namespace-qualified options class and every public instance
property in a complete named scope. Types and defaults come from the actual Roslyn compiler
model. Export never loads a publisher assembly or executes its constructors, initializers,
registration, storage or identity code. Schema1 retains its primitive and string-array subset
and original output meaning. Existing packages and emitted contracts remain readable.

Schema2 explicitly adds nullable values, native TimeSpan factory constants, source-local
parameterless auto-property objects, ordinal string-key dictionaries and native list/set graphs.
Unsupported constructors, computed defaults/accessors, inheritance, partial classes and dynamic
callbacks require a separately qualified owning producer. BCL names are checked against referenced
framework symbols; a source type with the same name cannot claim framework default semantics.
Duplicate set initializers are rejected according to their native comparer, rather than emitting
an array that differs from the compiled set. Collection immutability is not implied by metadata.
Set collection expressions support only the empty form; nonempty sets need an explicit native
constructor and comparer. Interface collection properties must construct an admitted native
list or set, and object/dictionary initializers must construct their exact declared concrete type.
Derived classes and custom collection constructors cannot borrow the declared type's defaults.

Schema2 contracts include `typeName` and `appliesTo`: explicit host, selected shell feature or
code-construction applicability. Presence of an unused package does not activate its settings.
Paths, requiredness, constraints, binding, precedence and restart semantics remain owner-reviewed
declarations bound to the complete source inventory. Numeric and semantic admission need actual
validator qualification; hashes alone do not prove arbitrary semantic rules.

Schema2 imports select a precise dependency package, complete scope and compiler-resolved type.
Add `FoundationSettingsMetadataDependency` items pointing to its independently emitted metadata.
The task binds that document to the actual resolved implementation DLL, rather than its compiler
reference assembly. A same-shaped type cannot substitute for the named owning type. Output binds
the dependency version, metadata SHA256 and implementation assembly SHA256; receivers also verify
the immutable selected package bytes. Historical schema1 has no named type authority and cannot
authorize these imports.

Secrets have no exported default, including null. Secret-bearing nested object values are omitted
according to the owning type's declarations, and direct secret overrides reject. Literal defaults,
examples, enum and const values are prohibited anywhere under a secret constraint node. Imported
graphs are checked for native shape, nullable semantics and secret classification before any clone.

The current publisher declarations cover the Foundation authentication and profile option owners,
JSON code/binding/paging, PostgreSQL policy budgets, problem options, response policies/localization,
domain-event bounds, MCP, discovery and hosted-page settings. The neutral Host integrates its own
options and exact vendor configuration protocols separately. `InspectSettingsSourceGraph` reads
explicit metadata-only source snapshots with declared native compiler context, without compiling
them into runtime libraries. It supplies typed/default facts to that integration producer; its
inspection output alone establishes neither vendor publication authority nor complete binding
semantics. Host metadata records those source, binary and integration authorities explicitly.
`complete` always covers its declared scope, not every option ever exposed by an assembly.

Published Build0.2.0 and its schema1 remain immutable. Build0.3.0 adds schema2 as a separately
versioned tool; public availability requires its publication gates to pass. Metadata is
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
An explicit non-NuGet producer can use `ValidateCompiledAssemblyOnly=true` together with
`ValidateOnly=true` to recheck its existing compiled metadata without a package-output item.
This mode never emits or refreshes metadata and retains source, dependency and final DLL checks.
Ordinary pack validation still requires NuGet's actual final assembly output.
No-build package validation restores the SDK's generated global-using Compile items before
reading that compiler context; it does not invoke compilation. Disabled packaging skips the
package-only check, while an explicitly requested compiled-payload check remains available.

Direct metadata target invocation resolves Compile and CopyFilesToOutputDirectory;
SkipCompilerExecution and DesignTimeBuild cannot create or validate publisher provenance.
Admission caps: 1 MiB declaration, 512 sources/references, 1 MiB per source and 16 MiB
source total, 64 MiB per reference and 256 MiB reference total, 32 contracts,
256 settings/contract, 128 semantic strings, 4 Ki-character declaration text,
16 KiB constraints, 16 Ki-character string defaults, 256 default array items,
and 2 MiB emitted payload. Violations preserve existing metadata and packed output.
Schema2 additionally admits at most32 imports, depth16,4096 graph nodes,256 object fields,
and a conservative2MiB retained default allowance across imports and every subsequent clone.

Encoding admission is incremental: each setting is encoded before retaining another,
through one fixed non-growing 2 MiB IBufferWriter. Expanded defaults hold typed JsonValue
references rather than pre-serialized JsonElements. Final indented payload and constraints
also use that bounded encoder; oversized output fails during encoding, never after building
an unbounded encoded string. Final/packed assemblies are streamed through SHA256 after a
256 MiB file admission check. Installed tests cover one >4 MiB expanded array and 253 arrays
whose admitted source/declaration would expand past 1 GiB, preserving existing output.
