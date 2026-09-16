# BFF callback routing repair

Publication of this repair as 0.2.2 was approved separately after the candidate
validation below. Candidate hashes identify local diagnostic packages, not release
artifacts. A stable release is available only after the full Release workflow succeeds.

## Outcome

The callback defect remaining in published 0.2.1 is repaired in 0.2.2. The actual
Keycloak/Chromium fixture now completes login, permission checks, CSRF rejection,
local logout and browser-storage assertions using unpublished candidate packages.
The initial candidate investigation did not commit, push, tag or publish;
publication was authorized afterward. Base source is 0.2.1 commit `10cfdba3ea160878e68bc4e12359948ea2e5cebd`;
upstream was verified unchanged before editing.

## Cause and change

CShells prefers the owner of the matched endpoint when selecting shell services and
middleware. The BFF feature configured OIDC callback paths but did not register
endpoints for them. On a root-mounted shell, `/signin-oidc` therefore selected no
shell and never reached its authentication middleware. A new real-shell regression
failed before the repair with `GET /signin-oidc: callback did not reach OIDC (NotFound)`.

The BFF feature now registers its configured sign-in, signed-out and remote-sign-out
paths as exact GET/POST routes. OIDC middleware consumes protocol requests before the
endpoint delegate. The delegate rejects unconsumed requests with a safe 400 error.
The routes are anonymous protocol entry points, carry private/no-store metadata and
are excluded from API descriptions. Existing OIDC state, correlation, nonce, PKCE,
issuer, audience, signing-key, lifetime and cookie protections remain in place.

Failure redirects now retain Request.PathBase, preventing an error in a prefixed
shell from redirecting into another shell's root BFF error endpoint. Callback and
access-denied settings reject nonliteral paths, route parameters, escaped paths,
trailing slashes, duplicate paths and collisions with built-in session endpoints.
No host routing fallback, realm scope, authorization assertion or CSRF rule was relaxed.

## Regression and build results

- Locked solution restore passed; Release build passed with zero warnings/errors.
- All 14 focused Foundation validators passed.
- The expanded BFF probe passed against real root and two prefixed CShells instances.
  It covers default/custom paths, GET/POST sign-in dispatch, invalid state and missing
  correlation rejection, correct challenge redirect URIs and S256 PKCE, signed-out
  callback dispatch, remote-sign-out dispatch, safe unconsumed-request fallback,
  unknown shells, exact HTTP methods, privacy and exclusion from API descriptions.
- Configuration tests reject malformed/ambiguous callback paths. Existing scope,
  locale and development/production cookie tests still pass.
- Final test-only additions were verified with the focused BFF probe after the full
  suite. Runtime candidate code was unchanged during those additions.

## Real provider evidence

An isolated copy of the maintained Program Kit bootstrap-identity fixture used the
published neutral 0.2.1 host and locally built 0.2.2-callbackfix.1 feature packages.
The provider realm, client, shell configuration, synthetic feature and browser
assertions were retained. The earlier failed consumer, receipts, and both failed
scope-candidate attempts remain untouched. No independent coding-agent trial ran.

The three recorded cases passed: pinned_provider_discovery, candidate_bff_activation,
and code_flow_permission_negatives_and_logout. Chromium returned
`PROGRAM_KIT_BFF_PROVIDER_OK`. The assertions establish:

- anonymous API 401 and accessible public session status;
- completed confidential code flow for both fixture personas;
- authorized API 204 and wrong-role API 403;
- empty localStorage/sessionStorage and HttpOnly/SameSite=Lax local session cookie;
- logout without CSRF token returns 400 and preserves the session;
- logout with the issued CSRF token clears the local session.

This is local-development candidate evidence, not a published-release receipt or a
production HTTPS/security-assurance result. The maintained browser fixture checks
local session termination; it does not follow and prove provider-wide SSO logout.
The HTTP regression separately verifies OIDC signed-out callback dispatch.

## Exact artifacts

- Candidate package family: 25 packages at `0.2.2-callbackfix.1` under
  `artifacts/callback-routing-2026-09-16/packages`. This diagnostic version is unpublished.
- BffCookie nupkg SHA-256:
  `848bd8098225b4a6a7511f35cb37722f57a858f4d91c1098103a86a5e74a89b7`.
- All package hashes: `artifacts/callback-routing-2026-09-16/candidate-packages.json`.
- Published host: `ghcr.io/orbyss-io/foundation-host@sha256:ec444b80371972d1db71b77918f002921d9712de5af6a19bc9e7cc5019ad5c28`.
- Provider: `quay.io/keycloak/keycloak:26.7.3@sha256:ff4257d0d64efbe99ed1ddfaf07765cc3c36dc7518bf8324d41961327f441c54`.
- Browser: maintained Playwright 1.62.1, installed Chromium, Node 20.11.1.
- `artifacts/callback-routing-2026-09-16/fixture/compatibility-results.xml` records the
  three passing cases; `command-7.stdout` records browser completion. The fixture
  also preserves locked dependencies, runtime-package-inputs.json, exact configuration,
  command streams and runtime-inputs.json. Disposable containers/networks were removed.
- Full validator output: `artifacts/callback-routing-2026-09-16/validators.log`.
- Release build output: `artifacts/callback-build.log`.

After separately approved publication, Program Kit must update verified artifact pins
and rerun its maintained release-bound fixture. Local candidate results are not a
replacement for that receipt. Foundation source and required validators remain
independent of Program Kit; the copied integration fixture is ignored local evidence.
