# Orbyss.Foundation.Identity.Keycloak.Admin

Keycloak Admin REST adapter split into opt-in CShell features. Portable application workflows use
the interfaces from `Orbyss.Foundation.Identity.Admin.Abstractions`; Keycloak-only administration stays
behind explicitly named Keycloak interfaces.

Available shell features:

- `Orbyss.Foundation.Identity.Keycloak.Admin.Users`
- `Orbyss.Foundation.Identity.Keycloak.Admin.Applications`
- `Orbyss.Foundation.Identity.Keycloak.Admin.Scopes`
- `Orbyss.Foundation.Identity.Keycloak.Admin.Access`
- `Orbyss.Foundation.Identity.Keycloak.Admin.Enrollment`
- `Orbyss.Foundation.Identity.Keycloak.Admin.Sessions`
- `Orbyss.Foundation.Identity.Keycloak.Admin.AuthenticationFlows`
- `Orbyss.Foundation.Identity.Keycloak.Admin.IdentityProviders`
- `Orbyss.Foundation.Identity.Keycloak.Admin.Organizations`
- `Orbyss.Foundation.Identity.Keycloak.Admin.RealmOperations`
- `Orbyss.Foundation.Identity.Keycloak.Admin.Realms` (unreleased source addition)
- `Orbyss.Foundation.Identity.Keycloak.Admin.Configuration` (unreleased source addition)

Configure `Foundation:Identity:Keycloak:Admin`. Use a confidential service-account client with only
the realm-management permissions required by the enabled features. The adapter intentionally does
not support admin username/password authentication.

The unreleased standalone composition, typed realm administration and effective-role additions
are described in [runtime configuration and identity administration](../../docs/runtime-configuration-and-administration.md).
They reuse the same adapter and require separately scoped machine credentials.
