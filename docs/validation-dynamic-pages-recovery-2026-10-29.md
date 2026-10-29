# Dynamic pages and recovery implementation review

Date: 2026-10-29. Status: implemented and locally exercised; unreleased. The results
below record pre-commit validation; subsequent local bundle outputs identify their actual commit.

Checkout: `C:/Users/tech_/Code/program-kit/artifacts/worktrees/foundation-dynamic-pages`.
Branch: `codex/dynamic-pages-recovery`, based on Foundation
`01083f77688b19836b850afb22925e4aa2b6f36c`. The original Foundation main checkout and
the user's existing Program Kit changes were not edited. VERSION remains the inherited
development value 0.3.10; local archives with that version are not public-release evidence.
EetRadar's selected published 0.3.9 baseline and consumer locks remain unchanged.

## Review the contracts

1. [DynamicPages](../src/Orbyss.Foundation.Web.DynamicPages/README.md): separately selected
   settings routes, scoped consumer resolvers, native BFF/permissions, safe initial metadata
   and inert JSON, optional full layout, response/deadline bounds and mandatory final current
   version/eligibility verification. Inspect the resolve/render/verify/write order in
   FoundationDynamicPagesFeature.cs and the actual race/cancellation cases in its probe.
2. [Presentation](../src/Orbyss.Foundation.Web.Presentation/README.md): existing released
   IProblemPageRenderer remains supported. New locale catalogs and full recovery layout
   preserve native forms, session observation, machine contracts and local-first logout.
   Custom layouts receive only bounded fields and generated native controls, including the
   normal HTML antiforgery field. They remain trusted code with encoding obligations.
3. [Adoption boundaries](dynamic-page-and-recovery-adoption.md): consumer publication,
   tenancy/ownership and data projections remain consumer-owned. There is no generic
   revocation of already delivered bytes or a replacement authentication lifecycle.

HostedPages' deployment contract is unchanged. Its stale README availability wording was
corrected. New current source manifests/settings declarations were refreshed through the
maintained metadata tool. Runtime package inventory and release checks include the new
31st optional package. New browser scenarios are wired into existing CI/Release paths.

## Observed local validation

- Locked solution restore and Release solution build passed with .NET SDK 10.0.403,
  ASP.NET Core 10.0.14 and explicit `FoundationMetadataPython=C:/Python315/python.exe`.
- The final focused DynamicPages build/probe passed: public-only and BFF composition,
  missing/invalid access and resolver selection, sparse routes, shell isolation, permission
  rejection, no redeploy publication changes, actual post-render unpublication barrier,
  stale version, conditional requests, HEAD, output-cache exclusion, caller cancellation,
  timeout/failure rejection, encoded metadata/bootstrap, output bounds and custom layout.
- Existing ProblemDetails, HostedPages, WebDefaults and complete native BFF probes passed.
  Native renewal/logout/expiry preservation, provider-failure provenance, cancellation and
  machine JSON contracts were retained. HostedPages reports local symlink creation unavailable.
- New recovery probe passed weighted/parent/fallback locale selection, translated native
  errors, accurate language headers, custom layout through native antiforgery/challenge,
  existing-session recovery, broken/oversized layout fallbacks and machine preservation.
- All 31 runtime NuGet archives packed locally. Package metadata, compiled settings-owner
  validation and publisher knowledge helper unit tests passed. The latter test's first run
  failed on the sandbox temporary directory; selecting a workspace-owned temp directory
  resolved the environment limitation without changing assertions.
- Actual neutral Nuplane Host cold package consumption passed, including public/private
  HostedPages, independent API permissions/ownership, native logout, the new dynamic-page
  resolver, removal after unpublication without redeployment, and localized recovery.
  Final runtime evidence: `artifacts/hosted-package-consumption/run-h7dqalot/result.json`;
  package and Host byte hashes and preparation logs are retained in that directory.
- Chromium 156.0.8078.4 passed new dynamic-page and recovery-localization browser scenarios
  and the existing native-presentation scenario. Initial metadata, script-end injection,
  inert bootstrap, native prefixes, mobile sizing, custom layouts, actual recovery focus,
  native challenge and no-script forms were exercised. Browser observations/screenshots
  remain under `artifacts/dynamic-pages/browser` and `artifacts/browser-presentation`.
- Locked TypeScript 7.0.2 type checking passed using its direct installed entrypoint;
  25 existing browser module tests passed. `npm run test:types` initially failed because
  this Windows npm subprocess could not resolve tsc; no production workaround was added.

The first dynamic fixture attempt did not discover its non-public test feature. Making
that fixture public and explicitly selecting the package corrected the harness; invalid
configuration tests now reject actual selected features. The first full build could not
resolve Python from the Host's Windows subprocess; the explicit installed Python path
corrected the invocation. These failed attempts are not presented as product successes.

## Remaining qualification

Installed Node is 26.12.1; the inherited development pin is 26.13.0. No toolchain was
installed. Chromium observations are local evidence, not exact pinned CI qualification.
WebKit is absent locally. Firefox has the known local-host launch limitation and was not
retried. The affected Linux CI scenarios retain Chromium, Firefox and WebKit. Linux,
symlinks, image scans and public installation were not qualified in this session.

Before public release: run successful exact-source native package CI,
complete normal dependency/release maintenance and applicable security/image gates, and
obtain publication authority. Local validation did not dispatch workflows, create tags or publish packages.
Refresh the versioned publisher knowledge from those actual artifacts, then qualify a new
Program Kit profile and consumer adoption. This source review does not establish whether
EetRadar previously received or overlooked the existing IProblemPageRenderer knowledge.
