# Orbyss.Foundation.Authentication.SpaPkce

The Orbyss Foundation direct SPA-PKCE API profile packaged as one CShells middleware feature. It owns
JWT bearer validation, exact-origin CORS, canonical permission enforcement, and stable
authentication failures. Activate `Orbyss.Foundation.Authentication.SpaPkce` in exactly one shell
authentication profile. The API metadata backchannel may use the shared optional
`BackchannelAuthority`; browser discovery and token issuer validation remain public-authority based.
