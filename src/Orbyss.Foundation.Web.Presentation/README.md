# Browser presentation

Select `Orbyss.Foundation.Web.Presentation` independently in a shell. It composes
WebDefaults, ProblemDetails and Json.AspNetCore. Foundation stays independent of
application tooling. The native Host retains a neutral document-navigation fallback
when no application endpoint is selected; it cannot recover an unreachable server.

Configure `Foundation:Web:Presentation` with `ApplicationName`, `Locale`, bounded
`Copy` overrides, a literal `RoutePrefix` (default `/auth`), local `ReturnPath`,
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
const selected = await (await fetch('/auth/configuration')).json();
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

The native browser fixture separately verifies private no-store pages and a public
static client shell with no private response payload. The latter adds synthetic
private DOM after actual native authentication, then revokes the native server
ticket without changing browser cookies and navigates back through real history.
Its Chromium variant removes Playwright's explicit BFCache-disable default and
requires an observed persisted restore. Other engines report their actual restore
mechanism; a fresh navigation is never labelled a BFCache restore. Native private
response cache policy is preserved throughout.
