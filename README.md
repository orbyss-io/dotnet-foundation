# Orbyss Foundation for .NET

Reusable, application-neutral .NET building blocks maintained by Orbyss. The repository owns the
runtime implementation, tests, packaging, and release lifecycle for the Foundation package family.

Foundation is not the Program Kit AI extension. Program Kit consumes released Foundation packages
and carries the architectural knowledge needed to select and compose them.

## Package families

- `Orbyss.Foundation.Authentication.*` — provider-neutral authentication and OAuth building blocks.
- `Orbyss.Foundation.DomainEvents.*` — awaited in-process domain-event contracts and dispatch.
- `Orbyss.Foundation.Identity.*` — provider-neutral identity administration plus opt-in adapters.
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
```

Stable tags must exactly match `VERSION`. The release workflow publishes the NuGet family through
NuGet.org trusted publishing and then publishes `ghcr.io/orbyss-io/foundation-host`.
