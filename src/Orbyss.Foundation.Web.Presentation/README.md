# Browser presentation

Select `Orbyss.Foundation.Web.Presentation` independently in a shell. It composes
WebDefaults, ProblemDetails and Json.AspNetCore. Foundation stays independent of
application tooling. The native Host retains a neutral document-navigation fallback
when no application endpoint is selected; it cannot recover an unreachable server.

Configure `Foundation:Web:Presentation` with `ApplicationName`, `Locale`, bounded
`Copy` overrides, the owning application's blue/green/slate `Theme`, a literal `RoutePrefix` (default `/auth`), local `ReturnPath`,
`ResponsePolicy`, `AssetResponsePolicy` and `SignOutConfirmation`. Unknown settings,
copy keys, paths, policies and confirmation modes fail activation. Native CShells
validates mapped route collisions before generation publication. Keep presentation
routes distinct from application pages and native API/control/callback routes.

The feature maps public GET/HEAD pages `sign-in`, `sign-out`, `signed-out`, `expired`
and `denied` below its prefix. Its admitted problem renderer handles explicit browser
exceptions/statuses, and a last-priority unmatched-route handler admits native
document navigation only. An Accept header alone never reclassifies an unknown or
machine endpoint. A custom `IProblemPageRenderer` can replace presentation, but
receives only bounded admitted fields and an independently observed local-session
state. It never receives raw exceptions, request text, tokens or provider responses.

With BFF-cookie authentication, set native `Foundation:Web:LogoutLandingPath` to
`/auth/signed-out` (or the selected prefix). Both browser forms validate native
antiforgery and reuse the native challenge/local-first logout operations. The
machine routes `/bff/user`, `/bff/antiforgery` and `/bff/signed-out` retain their JSON
contracts. Landing pages read the actual validated local session. Neither a landing
URL nor `remote=unavailable` establishes provider-session termination. Public assets
remain anonymous; recovery pages and actions are private, no-store and no-index.
The native forms also work without JavaScript.

GET `/auth/configuration` returns the bounded native JSON bootstrap
`foundation-browser-presentation/v1`: selected application surface, confirmation
mode, shared module and native BFF transport paths. It contains no session proof or
secrets. Browser files and checked ESM types are embedded in the assembly, included
in the package's `browser/` folder and served at `/auth/assets/v1/`.

```js
const { loadBrowserPresentation } = await import('/auth/assets/v1/configuration.mjs');
const selected = await loadBrowserPresentation('/auth/configuration');
const { unsavedWork } = await import(selected.modulePath);
const { prepareBffSignOut } = await import(selected.bffModulePath);
const unregister = unsavedWork(selected.surface).register({ getState: () => 'unknown' });
const control = await prepareBffSignOut({ surface: selected.surface,
  mode: selected.signOutConfirmation, antiforgeryPath: selected.antiforgeryPath,
  logoutPath: selected.logoutPath });
// Enable the invoking button only after preparation succeeds.
button.onclick = () => control.request({ returnFocus: button });
// Component disposal: unregister(); control.dispose();
```

`loadBrowserPresentation` bounds the native configuration read, validates exact
module/transport paths against the configured route and native shell surface, and
returns its checked immutable ESM contract. It acquires no antiforgery token and
proves no session state. Import the package's declarations for strict application
type checking; keep runtime modules served by the selected native feature.

Each editable component supplies synchronous `clean`, `dirty` or `unknown` state.
Pending saves are unknown. The registry neither saves nor discards work. Controls
in one window/application surface share policy and one pending request.
`when-unsaved` is the default; `never` bypasses source evaluation; `always` confirms
even clean state. Cancel preserves work. Confirm invokes the secure action directly
in its fresh event stack. Repeated controls do not duplicate dispatch. Automatic
session expiry is a separate recovery operation, never a confirmation bypass.

Direct SPA authentication remains owned by its selected browser protocol client:
the server cannot observe credentials held only by that client. Native recovery
pages explicitly show unconfirmed state and return to the application; they do not
pretend to perform a cookie logout. `spa.mjs` supplies `createSpaSignOutControl`
around that client's adapter. Synchronous local clearing and an observed anonymous
state precede the client's native provider-logout operation on the same gesture.
Async clearing is rejected. Provider dispatch/failure is an observation, not proof
of provider termination. No token or draft is stored in browser persistence by the
shared modules. Applications own their client initialization and domain data cleanup.

Source module checks, strict types, real browser tests and configured native BFF
probes are separate evidence scopes. Publication and installed-package/Host/profile
qualification require their own exact built artifacts; source probes do not certify
a consumer application or a public release.

For private client-rendered content, `session.mjs` supplies `observeBffSession`.
Declare the component's private DOM roots before adding private data. It hides and
makes them inert synchronously on page hide, and keeps them hidden until a fresh
bounded `/bff/user` observation confirms the native local session. Anonymous,
unavailable, malformed and superseded observations never reveal the boundary.
Page show and return to a visible document revalidate; disposal cancels reads and
leaves roots hidden. Applications render recovery from `onObservation` and own
component disposal. The guard stores no token/draft, discards no work, repeats no
domain request and initiates no logout.

`observeSpaSession` in `spa.mjs` uses the same private DOM lifecycle with a
selected client's fresh `observe(signal)` callback. Every observation has a finite
deadline (5 seconds by default, at most 30 seconds). The callback must propagate
the abort signal to its network reads; errors, timeouts and late results keep the
boundary hidden. `onObservation` must complete synchronously before private roots
can be revealed. A cached client user alone is not fresh provider-session proof.
Choose and document the actual session/revocation observation supported by the
provider; an unavailable observation does not establish local or remote logout.

The separate `tests/browser/spa-client-browser.cjs` fixture exercises the locked
`oidc-client-ts` client, its native authorization-code/PKCE popup and native popup
logout against a synthetic provider on another origin. Its explicitly owned
in-memory user and protocol-state stores are cleared synchronously before native
logout dispatch; the application projection is independently checked anonymous.
A one-use ID-token hint stays in the protocol call stack, outside persistent
storage. Applications must evaluate their own client's store and cleanup contract.
The fixture verifies fresh userinfo revocation, failed reads, real deadline expiry,
native nonce rejection, exact retained invalid draft text and zero pre-confirmation
provider effects. This does not establish immediate revocation for every JWT or
provider topology. Its popup-compatible application COOP policy is
`same-origin-allow-popups`, with no provider COOP header; an application using a
different isolation policy needs its own selected-client integration evidence.

The native browser fixture separately verifies private no-store pages and a public
static client shell with no private response payload. The latter adds synthetic
private DOM after actual native authentication, then revokes the native server
ticket without changing browser cookies and navigates back through real history.
Its Chromium variant removes Playwright's explicit BFCache-disable default and
requires an observed persisted restore. Other engines report their actual restore
mechanism; a fresh navigation is never labelled a BFCache restore. Native private
response cache policy is preserved throughout.

`prepareBffSignOut` registers and validates the control without network acquisition.
Only the shared admission callback starts native antiforgery acquisition. Cancel
therefore causes neither a token request nor logout. The admitted action validates
the bounded response and submits the native same-window form; it requires no popup
or delayed popup gesture. Controls remain serialized through the page transition.
A pending or observed navigation never certifies local/provider logout completion.
