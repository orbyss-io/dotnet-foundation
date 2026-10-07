# Contributor instructions

- Foundation owns runtime source, tests, packaging, and publication; it must not depend on Program Kit.
- Keep provider-neutral contracts free of provider-specific implementation details.
- Keep `Orbyss.Foundation.Host` application-neutral; behavior belongs in independently selected features.
- Keep .NET projects directly under `src/` and repository tooling under `scripts/`; do not introduce `src/dotnet` or a repository-level `eng` directory.
- Run locked restore, Release build, and the focused validators before tagging.
- A stable tag is only available after the complete Release workflow succeeds.

## Dependency, tooling and image maintenance

Before selecting any runtime or independent-tool release candidate, run the update workflow in
`docs/dependency-maintenance.md`: upgrade active external pins and lockfiles, then run all
deterministic tests and rebuild versioned publisher knowledge. Keep `maintenance-policy.json`
complete when introducing tooling. Do not add update surveys to ordinary edit loops. Upstream
lookup, build, test and security failures stop the update and identify what needs repairing.

Keep both Host Dockerfiles on the same reviewed runtime digest. Scan the actual candidate payload
for both published platforms before NuGet or image publication; preserve findings and stop on
unresolved High/Critical vulnerabilities. A newer SHA is not proof of remediation. Do not suppress
findings globally, mutate old published tags/digests, or silently publish a rebuild as existing
immutable evidence. Foundation owns a new Host release; downstream consumers adopt its new
digest through their own compatibility review. This repository must remain independent of Program Kit.
