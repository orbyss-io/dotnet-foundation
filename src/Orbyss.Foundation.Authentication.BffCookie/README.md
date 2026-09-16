# Orbyss.Foundation.Authentication.BffCookie

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
