# Orbyss Foundation for .NET

Reusable, application-neutral .NET building blocks maintained by Orbyss. The repository owns the
runtime implementation, tests, packaging, and release lifecycle for the Foundation package family.

Foundation is not the Program Kit AI extension. Program Kit consumes released Foundation packages
and carries the architectural knowledge needed to select and compose them.

## Package families

- `Orbyss.Foundation.Authentication.*` — provider-neutral authentication and OAuth building blocks.
- `Orbyss.Foundation.DomainEvents.*` — awaited in-process domain-event contracts and dispatch.
- `Orbyss.Foundation.Identity.*` — provider-neutral identity administration plus opt-in adapters.
- `Orbyss.Foundation.Json.*` — typed immutable JSON profiles and versioned canonicalization.
- `Orbyss.Foundation.Collections.Core` — shallow immutable sequences with ordered value equality.
- `Orbyss.Foundation.Execution.*` — monotonic deadlines and owned stage cancellation.
- `Orbyss.Foundation.PostgreSql` — native PostgreSQL composition and independent owned context factories.
- `Orbyss.Foundation.Mcp.*` — authenticated MCP transport composition.
- `Orbyss.Foundation.Tasks.*` — shell-lifetime task contracts and execution.
- `Orbyss.Foundation.Web.*` — web defaults, discovery, OpenAPI, and Problem Details features.
- `Orbyss.Foundation.Analyzers` — compile-time Foundation conventions.
- `Orbyss.Foundation.Host` — the application-neutral CShells/Nuplane host image.

## Local validation

```powershell
dotnet restore Orbyss.Foundation.slnx --locked-mode --configfile NuGet.config
dotnet build Orbyss.Foundation.slnx -c Release --no-restore
python tests/validate_analyzer.py
python tests/validate_bff_cookie_options.py
python tests/validate_assurance.py
python tests/validate_client_credentials.py
python tests/validate_token_exchange.py
python tests/validate_downstream_api.py
python tests/validate_dpop.py
python tests/validate_jwks_rotation.py
python tests/validate_domain_events.py
python tests/validate_keycloak_admin.py
python tests/validate_web_policies.py
python tests/validate_json.py
python tests/validate_hosted_pages.py
python tests/validate_openapi_exporter.py
```

Or run `python scripts/validate_foundation.py` after the Release build. Node 20 or newer is required
for independent canonicalization vectors. The 0.3.0 candidate adds six contract/provider packages
and retains existing package names. See [contracts adoption](docs/foundation-contracts-adoption.md)
for identity, bounded JSON, persistence, canonical-byte and migration guidance. Packaged conformance
also runs through the actual Host and a disposable PostgreSQL target before publication.

The Host build requires Python 3 for its offline native settings integration producer.
Use `FoundationMetadataPython` to select an explicit interpreter when necessary. Python is
build tooling; the published Host executes no settings exporter at startup.

Stable tags must exactly match `VERSION`. The release workflow publishes the NuGet family through
NuGet.org trusted publishing and then publishes `ghcr.io/orbyss-io/foundation-host`.

The exporter and `Orbyss.Foundation.Build` have independent versions. The runtime
publication gate packs exactly 30 runtime/analyzer packages with
`-p:FoundationRuntimeOnly=true`. An `exporter-v<version>` tag qualifies and publishes
only the exporter; a `build-v<version>` tag qualifies and publishes only the descriptor
build package. Both use the protected `release-tools.yml` workflow with exact source
version and single-package checks. Advancing either tool never republishes the other
or the runtime family. Configure NuGet trusted publishing for that workflow before
its first publication; CI packing supplies no public availability authority.

Publisher projects own canonical `orbyss-foundation/feature.json` descriptors.
The build package verifies reviewed source bindings during packing. Its immutable
legacy bridge covers descriptor-less Foundation 0.2.2 and 0.2.3 packages only.
Changing metadata in a runtime package requires a new immutable runtime release;
never replace an already published package at the existing `VERSION`.

Published Build0.2.0 introduced source-bound, bounded `orbyss-foundation/settings.json`
metadata for the named `JsonProfileSettings` scope. Its schema1 meaning remains unchanged.
The additive schema2 producer covers real nullable/container settings, named dependency
imports, secrets and applicability to selected features/configuration. Defaults derive from
owning source and compiled dependency metadata without loading publisher assemblies or
running initializers. See the [Build package](src/Orbyss.Foundation.Build/README.md).

The Host assembles transport, boot, CShells and Nuplane settings into
`.orbyss-foundation/host-settings.json`. The native integration scopes bind the exact selected
vendor archives, DLLs, source commits and metadata-only source snapshots. They describe the
Host's real native binding behavior; they do not claim vendor publisher emission or derive
defaults from sample configuration. No-build publication revalidates typed metadata and
source/assembly provenance without refreshing it. Feature descriptors remain the activation
authority, and settings metadata does not initialize applications, storage or identity clients.
