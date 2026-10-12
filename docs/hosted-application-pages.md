# Hosted application page contract and adoption

This contract belongs to Foundation 0.3.3, an additive HostedPages extension based on main
`8e3576d0b8dc25b820e56f4013d92137e410a440` (Foundation 0.3.2). Its availability requires the complete
protected [0.3.3 Release workflow](https://github.com/orbyss-io/dotnet-foundation/actions/workflows/release.yml)
to succeed, with immutable packages, Host image and versioned publisher knowledge published together.
The released 0.3.2 mechanism supports public PagePath hosting; naming a selection `private-pages` or
selecting a response policy does not confer authentication. No new Composition library, consumer Host,
domain package or provider-specific contract is required.

## Applicability catalog

| Requirement | Selection and limitation |
| --- | --- |
| Existing public semantic page | HostedPages without Routes; legacy PagePath and retained revision bootstrap remain supported. |
| Multiple public/private application entries | Foundation 0.3.3 typed Routes contract; select a revision and explicit access for each resource. |
| Private HTML/bootstrap | Select Foundation BFF-cookie independently; Authenticated mode requires its validated session and optional canonical permission. |
| Stronger bootstrap permission | Bootstrap admission is the conjunction of Page and Bootstrap requirements. It cannot weaken a private Page. |
| Public immutable code assets inside a private app | Explicit public manifest assets plus AssetResponsePolicy. All served assets, including branding/fonts, must be safe for anonymous disclosure. |
| Private result data or downloads | Independently owned API/storage implementation with canonical permission and resource ownership; not supplied by HostedPages. |
| Discovery, sitemap, Markdown or OpenAPI export | Hosted page/bootstrap endpoints are excluded. Discovery consumes only a separate explicitly public catalog; private content must never enter it. |
| Generic templates, per-account bootstrap data, private JS/CSS, provider login journey | Outside this fixed, deployment-snapshot page contract. |

## Minimal shell configuration

Enable `Orbyss.Foundation.Web.HostedPages` and `Orbyss.Foundation.Authentication.BffCookie` in the shell's
Features map. Their declared dependencies supply WebDefaults, Authentication and typed JSON. Configure
the BFF authority, client, audience and secret through the existing Foundation:Web contract and approved
secret providers. The following is the shell-local `Configuration:Foundation:HostedPages` section;
replace deployment identities/hash with the actual trusted build's values.

```json
{
  "Root": "hosted-pages",
  "ManifestSha256": "<exact manifest SHA-256>",
  "CurrentRevision": "app-1",
  "AssetResponsePolicy": "public-asset",
  "Routes": [
    {
      "Id": "dashboard",
      "Path": "/dashboard",
      "Page": { "Mode": "Authenticated", "Permission": "workspace.dashboard", "ResponsePolicy": "private" },
      "Bootstrap": { "Mode": "Authenticated", "ResponsePolicy": "private" }
    },
    {
      "Id": "activity",
      "Path": "/activity",
      "Page": { "Mode": "Authenticated", "Permission": "workspace.activity", "ResponsePolicy": "private" },
      "Bootstrap": { "Mode": "Authenticated", "ResponsePolicy": "private" }
    }
  ]
}
```

The permission identities in this example represent cohesive application-owner constants. Do not make
fixed domain permissions freely configurable just to remove literals. Operational shell deployment
configuration selects those reviewed identities. A consumer endpoint uses the same owner permission:

```csharp
endpoints.MapGet("/api/activity/{id}", ReadOwnedActivity)
    .WithMetadata(new WebResponseMetadata(Private: true))
    .RequireAuthorization(PermissionPolicyNames.ForPermission(ActivityPermissions.Read));
// ReadOwnedActivity independently checks resource ownership and state before returning data.
```

Framework-owned C# identifiers are `HostedPageOptions.SectionName`,
`FoundationHostedPagesFeature.FeatureName`, `HostedPageRouteOptions`, `HostedPageAccessOptions`,
`HostedPageAccessMode`, `PermissionPolicyNames.ForPermission`, `FoundationAuthenticationProfiles.BffCookie`
and `WebResponsePolicyNames.Default/Private/PublicAsset`. Configuration paths and canonical application
permission identities retain their distinct owners.

## Admission and response behavior

Each Id and Path must be distinct under case-insensitive matching; paths are literal local absolute
routes, without trailing slash aliases, templates, encoding or traversal. At most 64 routes are admitted.
An omitted Revision selects CurrentRevision. Each explicit route admits bootstrap only for that revision;
there is no revision-only bootstrap alias in explicit route mode. Addresses have the shape
`/_foundation/bootstrap/{routeId}/{revisionHash}/{locale}.json`, including actual shell routing prefixes.
Knowing or copying another route's address does not satisfy its permission. Legacy public mode retains
the original revision-only bootstrap shape and retained revisions.

Page and Bootstrap each select Public or Authenticated, optional Permission and ResponsePolicy. Public
plus Permission is invalid. Bootstrap inherits Page session/permission requirements, then adds any
bootstrap permission. A private Page cannot select a public Bootstrap. A public Page may select a private
Bootstrap, but its server-rendered HTML text is public and must be safe for anonymous disclosure.
Private mode requires the independently selected BFF-cookie profile and registered cookie scheme;
Foundation Authentication alone is not a session. No bearer profile can substitute for that requirement.

Private endpoints have native session/permission authorization and Private response metadata, without
AllowAnonymous. BFF's existing challenge/forbid behavior is preserved: interactive entry/bootstrap
denials redirect; `/api` failures receive bounded authentication errors. The loader uses same-origin
cookies for private bootstrap and refuses redirects. Public bootstrap omits credentials. The loader
reports load/admission failures through localized text-only DOM operations.

ResponsePolicy resolves an existing shell WebDefaults policy independently for Page and Bootstrap.
AssetResponsePolicy independently selects the explicitly public assets' policy. Activation validates
unknown selectors before publishing any endpoint. The one existing final writer owns headers for
successful responses, denial, error and redirect; Private always forces no-store and noindex/nofollow.
Changing policies does not change access admission. All hosted endpoints are excluded from API
description; build-time export descriptors keep HostedPages dormant and composeForOpenApi false.
Discovery/export workflows must never turn deployment manifests or private data APIs into public inputs.

Unknown configuration members, missing selections, explicit empty/null/sparse collections, conflicting
PagePath/Routes, duplicate identities/paths, invalid modes/permissions/revisions/policies and missing
BFF composition reject shell activation before endpoints appear. A new shell generation revalidates
configuration and byte snapshots; existing sessions are not a grant to a recreated generation.

## Qualification and consumer adoption

Local synthetic source and packaged HTTP/browser validation establishes implementation behavior only.
The runtime candidate is 0.3.3; consumer pins/locks/images and accepted evidence remain independently
owned. No DS01 acceptance, provider journey, paid request or consumer deployment follows from these checks.

Before selecting a release candidate, run the repository dependency update workflow, review active
SDK/tool/runtime pins and both matching Host base digests, and commit the prepared source. Choose a new
immutable runtime version; do not republish 0.3.2 with different bytes. From the exact clean candidate:

1. Run `python scripts/update_dependencies.py --export-knowledge` after the required source preparation.
   Keep locked restore, Release build, all focused validators, real Linux PostgreSQL contracts, symlink
   rejection, installed tools/metadata checks and package/Host HTTP qualification in scope.
2. Run locked browser tooling and Chromium/Firefox/WebKit probes. Linux CI and Release require all three;
   a local Windows Firefox launch limitation is not a Firefox qualification pass.
3. Validate all 30 runtime packages, exact source-bound feature/settings metadata, no-build Host publish,
   packaged public/private hosting/BFF/data/logout behavior, and the portable Host payload provenance.
4. Export exact versioned publisher knowledge from the clean commit. Build both Host image platforms,
   scan their actual candidate payloads and stop on unresolved High/Critical findings. Do not publish
   before the protected Release workflow passes; only its complete success makes a stable tag available.
5. Verify NuGet propagation, immutable package hashes/source/version, Host digest/attestation and published
   knowledge. Update capability applicability to the newly qualified contract rather than attributing
   this source extension to the accepted 0.3.2 profile.

Once that exact qualified version exists, the consumer separately reviews/adopts the runtime family,
new Host digest, immutable publisher knowledge and compatible profile. Renew retained compatibility and
implementation gates and establish a fresh successful implementation preflight. Replace path rewrites,
manual Challenge/Forbid and competing header writers with typed route selection and native endpoint
metadata. Preserve canonical application permission constants and independent API ownership checks.
Run the selected Host at its actual origin: anonymous/missing/admitted permissions, both distinct entry
routes and cross-route bootstrap, separate API/asset admission, policy changes and invalid activation,
final headers on success/denial/error/redirect, logout/re-entry and shell replacement, private discovery/
export exclusion, public assets and prepared browser engines. Consumer story/DS01 review and acceptance
remain its own tasks and evidence.
