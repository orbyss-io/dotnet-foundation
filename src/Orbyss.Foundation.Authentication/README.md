# Orbyss.Foundation.Authentication

Shared CShells authentication composition for Orbyss Foundation. The feature binds shell-scoped web
configuration, validates that exactly one authentication profile is active, maps provider roles and
scopes to canonical application permissions, and supplies a replaceable authentication error writer.
`Authority` remains the issuer and browser-facing origin; deployments that need a different
server-side route may set `BackchannelAuthority` for metadata retrieval without weakening issuer
validation.

Applications activate a concrete profile feature rather than this package directly; the BFF-cookie
and SPA-PKCE features declare it as a dependency.

`Foundation:Web:Scopes` is a complete selection, not an addition to the defaults. For example,
`["openid", "profile", "offline_access", "consumer-api"]` requests exactly those four scopes.
When `Scopes` is omitted, the compatibility defaults remain `openid`, `profile`, `offline_access`,
and `orbyss-foundation-api`. Consumers with a different API scope should supply their complete list;
`["openid"]` is a valid minimal selection and does not implicitly request profile or offline access.

An explicit empty array, null, scalar, malformed indexed array, missing `openid`, duplicate, or
invalid token fails options validation. Each entry must be one case-sensitive OAuth scope token
([RFC 6749 section 3.3](https://www.rfc-editor.org/rfc/rfc6749#section-3.3)); whitespace, quotes,
backslashes and non-ASCII characters are rejected. Invalid selections never restore defaults.
These rules apply to the effective shell configuration after configuration-provider precedence:
standard IConfiguration providers merge indexed keys across sources, so deploy a complete shell
selection rather than relying on a shorter array in a later provider to delete earlier indices.
Recreate the shell to apply configuration changes to its authentication handlers.
