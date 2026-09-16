# Scope-binding repair: 0.2.1 release evidence

The user approved publishing this scoped repair as 0.2.1 after the candidate investigation
below. The separate callback-routing defect remains unresolved. Candidate package hashes
below describe the local diagnostic artifacts, not the subsequently built release packages.
The stable release is available only after its complete Release workflow succeeds.

## Diagnosis and repair

Upstream/main was verified at `3a97b158bd020391a0954e2148acc001eb8f2a17` (v0.2.0)
on 2026-09-16. The worktree was clean before this repair. The initial candidate investigation
did not commit, push, tag or publish; publication was authorized separately afterward.

The regression against downloaded 0.2.0 packages reproduced the effective OIDC list:
`openid profile offline_access orbyss-foundation-api program-kit-api`.
The configured selection was `openid profile offline_access program-kit-api`.
ConfigurationBinder appends to the initialized FoundationWebOptions.Scopes array;
the OIDC handler then removes duplicates but retains the unwanted publisher scope.

FoundationAuthenticationFeature now detects an explicit selection, validates its
indexed shape and clears only that fallback array before the normal registered
configuration binder runs. The existing options/change-token registration remains.
Omission preserves the four compatibility defaults. Empty/null/scalar/malformed
selections fail with OptionsValidationException. Missing openid, empty/null entries,
duplicate scopes and invalid OAuth scope tokens fail validation. A minimal openid-only
selection remains minimal. Token syntax follows RFC 6749 section 3.3.

The same experiment proved SupportedLocales appended en to an explicit nl/de list.
WebDefaults now replaces that explicit selection and rejects empty/null/malformed,
blank or duplicate locales. Its default en list remains on omission, and the selected
list must still contain DefaultLocale. No authentication protocol, validation,
antiforgery, session-store or cookie behavior was changed.

## Regression evidence

The maintained BFF probe now uses real JSON, IConfiguration, the pinned CShells
ConfigurationShellBlueprint loader, feature service registration, IOptions and the
effective named OpenIdConnectOptions. It covers explicit and minimal selections,
omission, invalid selections, repeated resolution, fresh options creation, reapplying
the registered binding, independent shell service providers and locale recreation.
It checks PKCE, code flow, issuer/audience/signature/lifetime validation, and retains
the existing development/production cookie checks. The same probe fails against
published 0.2.0 at the effective OIDC scope assertion and passes against the repair.

Locked solution restore and Release build passed. All 14 Foundation focused validators
passed; the final expanded binding probe also passed after its last test-only changes.
This includes JWKS refresh/rotation, authentication boundaries, BFF cookie invariants,
response policies and hosted-page shell isolation. New test dependencies use the
already pinned CShells runtime; production dependency graphs are unchanged.

Collection audit: Scopes and SupportedLocales were the only non-empty default arrays
in configuration-bound options. AllowedOrigins, role/scope permission mappings,
machine-token scopes, assurance arrays and JSON extensions start empty. The regression
checks exact configured origins and permission mappings. Response-policy and JSON
profile dictionaries intentionally retain named presets and bind per-name overrides;
they are not permission-selection lists. Security-policy binding assertions confirm
an override retains restrictive CSP/private defaults. Existing two-shell JSON tests
cover profile overrides. No speculative collection-wide replacement was introduced.

These rules operate on effective shell configuration. IConfiguration merges indexed
keys across providers; this repair does not redefine provider precedence. Shell
configuration is a snapshot; recreate the shell to apply changed handler settings.

## Real provider rehearsal: separate blocker remains

An isolated copy of Program Kit's maintained bootstrap-identity fixture was used.
The original failed consumer, receipts and source fixture were not edited. Realm,
client, shell settings, synthetic feature and browser assertions were retained.
Only package references changed to the local candidate, and the activation case was
relabeled candidate_bff_activation. A second preserved attempt added diagnostics only.
No unwanted realm scope was registered; no assertion was removed or weakened.

Both attempts passed pinned Keycloak discovery and candidate BFF activation. Keycloak
accepted the scope selection and displayed the login form. After credentials, both
attempts stopped at /signin-oidc. Host diagnostics identify the separate cause:
`No shell matched the request ... PathFirstSegment=signin-oidc ... Candidate blueprints: []`.
FoundationBffCookieFeature does not register its OIDC callback paths as shell endpoints;
the neutral host's path routing therefore does not enter the shell authentication
middleware for this callback. This is a separate routing defect, outside the scoped
collection-binding repair, and requires its own repair and regression coverage.

Anonymous 401 and public /bff/user checks passed before login. Successful callback,
authorized/wrong-role responses, CSRF rejection, logout and empty browser storage
were **not proven** by this run because the callback blocked those assertions.
There is no passing Program Kit compatibility receipt or end-to-end claim.
Disposable test containers/networks were cleaned up by the fixture.

## Exact artifacts for follow-up

- Candidate family: 25 local packages at `0.2.1-scopefix.1`, built from the repair
  worktree on base 3a97b15. This is an unpublished diagnostic version, not an approved
  release version. Later edits only expanded tests, documentation and XML summaries.
- Package directory: `artifacts/scope-binding-2026-09-16/packages`.
- Complete package identities and SHA-256: `artifacts/scope-binding-2026-09-16/candidate-packages.json`.
- Authentication SHA-256: `8cbedf879314cfd85eec4f63b420b5b54abbef342a2224ff3bbdddbe720a6c88`.
- BffCookie SHA-256: `b5dae82c3c9bcbc4a197bb7f2fb6384e0de809089bef076870f62b6389f039f3`.
- WebDefaults SHA-256: `1ed3ef84590f55c65e6d4a9e990fb3e13469c7592fca8646ad4f3a181c3fbcef`.
- Neutral host: `ghcr.io/orbyss-io/foundation-host@sha256:78d58af0179c58355e969b42f884710ffd305b835fe8c01a1aa2627f0f277866`.
- Provider: `quay.io/keycloak/keycloak:26.7.3@sha256:ff4257d0d64efbe99ed1ddfaf07765cc3c36dc7518bf8324d41961327f441c54`.
- Browser tooling: maintained Playwright 1.62.1 and installed Chromium; Node 20.11.1.
- Evidence root: `artifacts/scope-binding-2026-09-16`; baseline.log records the published
  regression failure, validators.log records the focused suite, fixture-source-inventory.json
  hashes the source inputs, and fixture/ plus fixture-diagnostic/ retain separate commands,
  dependency locks, package inventories, browser diagnostics and failing XML results.

The real rehearsal combined the published neutral host with unpublished candidate feature
packages; it did not test a newly published Foundation release. After the separate callback
repair and explicitly approved publication, Program Kit must verify new pins and rerun its
maintained fixture against released artifacts. Do not substitute these local results for
that release-bound receipt.

## Local reproduction

```console
dotnet restore Orbyss.Foundation.slnx --locked-mode --configfile NuGet.config
dotnet build Orbyss.Foundation.slnx -c Release --no-restore
python scripts/validate_foundation.py
```

The optional Program Kit rehearsal remains an external integration investigation under
ignored artifacts; Foundation production and its required validators do not depend on
Program Kit. The source fixture's prepare/run inputs and diagnostic adaptations are
preserved in the evidence root for review.
