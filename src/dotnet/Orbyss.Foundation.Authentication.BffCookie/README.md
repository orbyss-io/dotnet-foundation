# Orbyss.Foundation.Authentication.BffCookie

The Orbyss Foundation confidential OIDC BFF profile packaged as one CShells web/middleware feature. It
owns cookie/OIDC authentication, server-side tickets, antiforgery validation, and `/bff/*` session
endpoints. Activate `Orbyss.Foundation.Authentication.BffCookie` in exactly one shell authentication
profile. The feature owns its OIDC metadata backchannel and honors the shared optional
`BackchannelAuthority` while preserving the public issuer authority.
