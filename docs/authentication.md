# Authentication boundary

## Provider-neutral capability boundary

Foundation authentication packages model capabilities rather than identity products. The runtime
contracts cover confidential BFF cookies, direct SPA-PKCE bearer validation, OAuth client
credentials, RFC 8693 token exchange, DPoP sender constraint, assurance/step-up (`acr`/`amr`), and
discovery/JWKS key rollover without depending on a provider-specific SDK.

Keycloak is the built-in local identity adapter and conformance fixture. It is intentionally outside
the provider-neutral authentication packages. A capability is not considered supported merely
because the Keycloak fixture can perform it; the portable contract and negative gates must exist in
the Foundation package family.
