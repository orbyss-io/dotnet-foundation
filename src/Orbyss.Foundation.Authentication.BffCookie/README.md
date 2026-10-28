# Orbyss.Foundation.Authentication.BffCookie

Session, antiforgery and signed-out JSON now use registered typed response contracts with the shell's
configured `success-response` budget. Anonymous/authenticated property sets, nullable display names,
validated account strings and distinct ordinal permissions are preserved. The BFF selects Json.AspNetCore
as a feature dependency; basic Authentication and Problem Details writing remain independent of global
exception-feature activation. Permission projection is admitted incrementally before an array is retained;
oversized output produces a safe 500 instead of a truncated success. Configured generated resolvers must
cover the public BFF response types as well as application DTOs, or use deliberately separate profiles.

The Orbyss Foundation confidential OIDC BFF profile packaged as one CShells web/middleware feature. It
owns cookie/OIDC authentication, server-side tickets, antiforgery validation, and `/bff/*` session
endpoints. Activate `Orbyss.Foundation.Authentication.BffCookie` in exactly one shell authentication
profile. The feature owns its OIDC metadata backchannel and honors the shared optional
`BackchannelAuthority` while preserving the public issuer authority.

The feature registers the configured `CallbackPath`, `SignedOutCallbackPath` and
`RemoteSignOutPath` as shell-owned GET/POST routes (defaults: `/signin-oidc`,
`/signout-callback-oidc`, `/signout-oidc`). These routes let CShells select the owning
shell before the OIDC middleware processes the request. They permit anonymous
protocol requests, require the handler's normal state/correlation/token validation,
use private/no-store response policy, and are excluded from OpenAPI descriptions.
If the OIDC handler does not consume a matched request, the route returns a safe 400
`authentication_callback_invalid` response rather than accepting authentication.

Callback and access-denied paths must be distinct literal absolute paths relative to
the shell, without route parameters, wildcards, query strings, fragments, escapes,
dot segments or trailing slashes. They cannot replace built-in `/bff/*` session routes.
For a shell with `WebRouting:Path: "tenant"`, retain `CallbackPath: "/signin-oidc"`
and register the resulting `/tenant/signin-oidc` redirect URI with the provider.
Protocol failure redirects retain this shell path prefix.

OIDC projects issuer and subject from the validated token into the public reserved claim types,
replacing token-supplied reserved values. Ticket admission then requires the exact projection claims
to survive user-info claim actions: removal, replacement, duplication or reintroduction fails the
callback. Cookie issuance, cookie revalidation and `/bff/user` use the shared validated identity reader.
Issuer and subject values are never normalized. Application ownership remains application policy.

Server-held ticket keys are bound to the owning shell even when shells share a distributed cache.
Foreign keys cannot be read, renewed or removed through another shell's ticket store. Existing
validated claim URNs remain compatible. Older unbound session keys cannot establish shell ownership
and therefore require a new login after this update; no account or persisted application identity changes.

Session creation and renewal are distinct. Only a new login creates an opaque key and a new absolute
deadline. Renewal requires a live existing server entry, preserves that entry's original absolute
deadline and cannot recreate a ticket removed by logout or idle/absolute expiry. For the default exact
`MemoryDistributedCache`, native creation, conditional renewal and removal share synchronization
across all ticket-store instances using that cache instance. A request authenticated before logout
can still finish and emit a late opaque cookie; that cookie cannot restore the removed server session.

Custom `IDistributedCache` replacements must also implement `IBffSessionTicketCache` on the registered
cache. Its `TryRenewAsync` must atomically compare the current live bytes and replace the entry,
ordered with expiry/removal and independent clients in the actual backend. Unsupported replacements
fail explicitly when resolving the native ticket store; no process-local lock is claimed to protect
a remote backend. A competing renewal can win the comparison; subsequent requests use the current
server ticket. No Redis or other remote provider is supplied by this package. Existing custom-cache
integrations need that native capability before adopting this correction; shell key ownership and
the wire cookie/session-key format remain unchanged. Ordinary applications retain the default cache.

`RemoteAuthenticationTimeoutSeconds` defaults to 900 seconds, including human time entering
credentials, MFA and consent. Positive explicit overrides remain effective for both the remote flow
and native nonce lifetime. Expired state/correlation still fails safely and needs a fresh sign-in.
`DiscoveryTimeoutSeconds` remains a separate three-second per-HTTP-request backchannel timeout;
native metadata retries may consume more than one request budget. Session expiry is separate.

`LogoutLandingPath` defaults to `/bff/signed-out`, preserving its typed machine JSON acknowledgement.
Set it to an application-owned public page such as `/signed-out` or `/`. It is shell-relative and
uses the owning shell's captured authentication path base for both native provider completion and
initiation-failure fallback (`?remote=unavailable`). It must be a literal local path of at most 2048
characters, without query, fragment, escapes, dot segments or routing syntax. Protocol/error paths,
the API namespace and BFF controls are reserved; `/bff/signed-out` is the compatibility exception.
Local ticket/cookie deletion precedes provider navigation and logout still requires antiforgery.
Neither reaching a landing, its query hint nor the static machine acknowledgement proves current
local session state or provider-wide sign-out. Applications should observe `/bff/user` and distinguish
confirmed local sign-out from unknown provider completion when presenting recovery.

Applications replace the existing `IAuthenticationErrorWriter` through shell DI to present browser
recovery while retaining bounded machine/API problem responses. Initial sign-in discovery, JWKS and
native PAR transport failures, HTTP timeouts and availability statuses (408, 429, 5xx) reach that
writer as 503 `identity_provider_unavailable`. Classification uses failures observed by the owned
native transport/retriever, retaining native metadata refresh, key rollover and last-known-good
behavior. Malformed metadata, invalid client/address responses, unmarked event/programming errors
and caller cancellation keep their native or general error handling. Explicit custom HTTP clients
and configuration managers retain their ownership; opaque transport failures cannot be classified
by this provenance mechanism. Provider response bodies and exception messages are not logged by
the recovery handler.

A failed new attempt does not delete an independently valid existing session. Browser presentation
may offer Continue only after validating that session and must retain current permission checks.
For shell-local retry links in an endpoint writer, use
`context.Features.Get<IAuthenticationFeature>()?.OriginalPathBase ?? context.Request.PathBase`;
CShells restores the outer path before endpoint delegates execute. Branding and recovery UI belong
to the application feature, keeping Foundation.Host application-neutral.
