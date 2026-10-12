# Authenticated HostedPages implementation validation — 2026-10-12

Implemented in `C:/Users/tech_/Code/Orbyss/dotnet-foundation/.worktrees/reusable-consumer-foundations`,
branch `codex/authenticated-hosted-pages`, based on freshly fetched main
`8e3576d0b8dc25b820e56f4013d92137e410a440`. Implementation remains uncommitted working-tree changes;
the primary older checkout and its unrelated locks were not changed. VERSION remains 0.3.2. The local
package family is `0.3.3-local.authenticated-pages`, an unpublished development identity. Source hashes,
working-tree patch/snapshot, package hashes and preserved Host payload hashes are under
`artifacts/authenticated-hosted-pages/`; they distinguish these bytes from the released 0.3.2 contract.
No release, tag, deployment, consumer upgrade or DS01 acceptance is claimed.

## Evidence and reproduction

Read the supplied consumer feedback at
`C:/Users/tech_/Code/Orbyss/temp-tests/claim_evaluator/artifacts/program-kit-upgrade-0.13.0/local-repair/DS01-foundation-reuse-feedback.md`
and its exact captured publisher sources in `artifacts/cache/rm01-implementation/`: HostedPages feature,
options/catalog/README, authentication/BFF and response investigation 0–5. Their header identifies the
approved `foundation-0.3.2-build-0.3.1-exporter-0.2.5-forms-0.2.1-localization-0.1.2` profile. The read-only
tasks checkpoint has only T001 checked. No consumer source, pins, locks, image, approvals or evidence
were modified. These captures explain the approved capability mismatch; the repair is on current main.

`python scripts/reproduce_hosted_private_gap.py` exported exact baseline 8e3576d into an isolated
snapshot, added only a synthetic probe selection of the existing BFF plus private feature response
policy, restored/locked-restored/built it and ran HTTP. Anonymous `/a/private` returned HTML 200 and its
bootstrap returned 200. The baseline options lacked private route admission. Evidence and the complete
replayable baseline fixture are in
`artifacts/authenticated-hosted-pages/reproduction/baseline-4raw_kxr/command-0.log` through `command-3.log`.
An earlier focused reproduction was also captured before runtime edits in `reproduction.log`.

Root cause: HostedPages attached AllowAnonymous to HTML/bootstrap/assets, without access settings.
Response policies govern final headers and cannot override anonymous admission. The loader omitted
credentials; bootstrap addressing partitioned only shared content revision/locale. The existing public
feature worked as documented. Authentication and final response policy were already present.

## Repair and observed behavior

The [contract, applicability catalog and minimal example](hosted-application-pages.md) document typed
Routes with Id/Path/Revision and explicit Page/Bootstrap Mode/Permission/ResponsePolicy. Bootstrap
inherits Page admission and can add a permission; its URL includes route ID and revision hash. Private
endpoints have native BFF-cookie session and canonical permission authorization, Private response
metadata, no AllowAnonymous and API-description exclusion. Private fetches use same-origin credentials
and reject redirects. Public manifest assets retain independent public immutable admission and policy.
Data permissions and resource ownership remain independent application responsibilities.

The source HTTP fixture selects real CShells features by configuration and performs actual dispatch.
Synthetic cookie issuance uses the actual BFF store/identity admission; it is test-only. The installed
probe runs the actual Foundation Host, Nuplane package loading and independently selected shell
features. Its separate synthetic issuer fixture package is not part of the 30 runtime packages or Host.

| Required behavior | Observed check |
| --- | --- |
| Public compatibility | Legacy page/revision bootstrap, localized encoded HTML, imports, snapshots, HEAD/ETag and wrong-shell 404; explicit public route mounts. |
| Anonymous, missing and granted permission | Native BFF login/access-denied redirects for HTML/bootstrap; admitted sessions receive 200; session-only route also exercised. |
| Distinct routes/cross-route bootstrap | Alpha and beta share a revision but have distinct URLs; each permission denies the other's bootstrap; revision-only alias is 404. |
| Separate page/bootstrap/data/assets | Additional bootstrap permission denies staged load; public HTML can independently require private bootstrap; APIs enforce data.read plus owner/foreign-ID checks; anonymous public assets stay usable. |
| Final response headers | Success, denied/unauthenticated redirects, 401/403/404, handled private exception 500 and explicit redirect have managed security/cache/crawler headers; private overrides a policy permitting indexing/following. |
| Logout/re-entry | Actual BFF antiforgery/logout removes session; page and bootstrap re-entry deny in HTTP and both local browser engines. |
| Invalid configuration | 18 selections reject before any shell endpoints appear: missing/invalid modes, weak bootstrap, public permission, bad token/policy/revision, duplicate ID/path, unsafe paths, conflicting selectors, unknown setting, missing BFF, sparse/null/empty Routes. |
| Shell isolation/recreation | Foreign shell does not admit another shell's cookie; recreation revokes old sessions, changes Page and inherited Bootstrap permissions and preserves mapped shell URLs. |
| Private discovery/export exclusion | Actual ApiExplorer and OpenAPI output omit hosted page/bootstrap paths/content; explicit public discovery projection contains no private deployment text. Feature export remains dormant and composeForOpenApi=false. |
| Public immutable assets | JS/CSS/built-ins mount in private applications anonymously with immutable headers and no synthetic private marker; private manifest asset admission still rejects. |

## Exact checks and artifacts

Commands ran from the worktree. Windows used the bundled Python executable
`C:/Users/tech_/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe`; the full build
passed that path as FoundationMetadataPython. MSBuild node reuse/server use was disabled. Loopback and
NuGet auditing needed sandbox escalation; auditing was not suppressed.

- `dotnet restore Orbyss.Foundation.slnx --locked-mode --configfile NuGet.config -m:1 -nr:false` — passed.
- `dotnet build Orbyss.Foundation.slnx -c Release --no-restore --disable-build-servers -m:1 -p:FoundationMetadataPython=<above Python path>` — passed, zero warnings/errors; final normal-version build recorded in `release-build.log`.
- `python scripts/validate_foundation.py --postgresql-deadline-only` — the complete listed focused validator sequence passed. Follow-up `python tests/validate_hosted_pages.py` passed the final 18-case fixture; see `focused-validators.log` and `http.log`.
- `npm ci --prefix tests/browser --ignore-scripts` and `node tests/browser/hosted-pages.cjs --engines=chromium,webkit` — passed. Chromium **156.0.8078.4**, WebKit **27.2**; final results at `browser/results.json`.
- Development build used `-p:FoundationVersion=0.3.3-local.authenticated-pages`; runtime packing used `dotnet pack Orbyss.Foundation.slnx -c Release --no-build --no-restore -p:FoundationRuntimeOnly=true -p:FoundationVersion=0.3.3-local.authenticated-pages --output artifacts/authenticated-hosted-pages/nuget`. The source attribute identifier initially failed descriptor qualification; after restoring the producer's supported literal attribute declaration, the HostedPages project rebuilt/repacked successfully. All 30 archives passed `tests/validate_package_metadata.py --packages <development feed> --version 0.3.3-local.authenticated-pages`.
- `python scripts/validate_hosted_package_consumption.py --packages artifacts/authenticated-hosted-pages/nuget --version 0.3.3-local.authenticated-pages` — passed **26 private HTTP dispatch observations**, legacy public serving, immutable assets and exact package/Host hash verification. Evidence: `artifacts/hosted-package-consumption/run-4hl1we_q/{inputs,observations,result}.json`.
- `python tests/validate_settings_owners.py --packages <development feed> --version 0.3.3-local.authenticated-pages` — **18 packaged owners** passed compiled defaults, source/assembly/archive bindings and native admission; `artifacts/settings-owners/tmpyj57hlow/result.json`.
- `python tests/validate_settings_authentication_owner.py --packages <development feed>` — **80 cases** passed; no identity/storage client resolved. Evidence: `artifacts/settings-authentication-owner/tmpij09fyig/results.json`.
- `python tests/validate_web_policies.py --package <development WebDefaults archive> --bff-package <development BFF archive>` — installed final-writer/BFF probes passed; `artifacts/web-policies/packaged-l7dtlmy8/results.json`. The harness now uses exact supplied versions for the full development family; partial two-owner feeds retain stable fallback.
- `python tests/validate_web_policy_feed.py`, `python tests/validate_dependency_updates.py`, `python tests/validate_update_workflow.py` — **3, 9 and 2 tests** passed after tooling updates. `python -m py_compile` passed changed Python entry points; `scripts/refresh_feature_metadata.py` and `git diff --check` passed.

Source-bound feature/settings declarations include complete route and Page/Bootstrap access scopes.
Metadata refresh tooling updates both reviewed inventories. Playwright is pinned/locked at 1.64.0 and
registered in maintenance-policy.json; CI, Release and dependency qualification run all three engines
on Linux. Browser subprocesses use direct DLL launch to avoid orphaned `dotnet run` children.

## Limits and remaining qualification

Firefox cannot launch on this Windows host, so no local Firefox pass is claimed. Linux CI/Release must
run it alongside Chromium/WebKit. Windows lacks the symlink privilege; that one source-admission leg
remains required on Linux. The focused sequence exercised PostgreSQL deadlines, not native PostgreSQL.
The portable local Host ran successfully; no Docker image or platform security scan was performed.
Synthetic session tests are not an external provider login journey or consumer product acceptance.
No paid calls, real sensitive data or immutable release knowledge export was performed.

See [the exact qualification and adoption sequence](hosted-application-pages.md#qualification-and-consumer-adoption).
It requires dependency maintenance/source preparation, a new runtime version and clean source commit,
complete deterministic/Linux/native-provider/browser/package/Host checks, versioned publisher knowledge,
both matching Host base digests/platform payload scans and the complete protected Release workflow.
Only then can the consumer review/adopt package versions, Host digest and knowledge/profile, renew
compatibility/preflight evidence, replace its workaround and exercise its own selected-host checks and
requester DS01/story acceptance. Local implementation validation is not authority to change its pins.
