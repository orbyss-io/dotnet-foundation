# Orbyss.Foundation.Authentication

Shared CShells authentication composition for Orbyss Foundation. The feature binds shell-scoped web
configuration, validates that exactly one authentication profile is active, maps provider roles and
scopes to canonical application permissions, and supplies a replaceable authentication error writer.
`Authority` remains the issuer and browser-facing origin; deployments that need a different
server-side route may set `BackchannelAuthority` for metadata retrieval without weakening issuer
validation.

Applications activate a concrete profile feature rather than this package directly; the BFF-cookie
and SPA-PKCE features declare it as a dependency.
