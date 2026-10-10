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

`RemoteAuthenticationTimeoutSeconds` must be positive; invalid values fail shell configuration admission before the OIDC handler is challenged. The value remains operational and fixed per shell activation.
