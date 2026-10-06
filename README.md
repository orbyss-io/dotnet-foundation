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

Build0.2.0 additionally emits source-bound, bounded `orbyss-foundation/settings.json` metadata.
The first declaration covers only `JsonProfileSettings`; it does not describe effective Host,
authentication, PostgreSQL, CShells, Nuplane or Json.AspNetCore configuration. Its metadata and
the existing feature descriptor have separate schemas and authority.
