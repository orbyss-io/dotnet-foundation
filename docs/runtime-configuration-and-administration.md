# Runtime configuration and identity administration

Foundation 0.3.13 adds the capabilities described here. Consumers retain their selected public
packages and host image until its official Release workflow has completed and the published
artifacts have passed their own compatibility qualification.

## Standalone administration clients

`AddFoundationKeycloakAdministration` composes the same service-account transport and typed
clients used by the opt-in shell features. A maintenance console or background process can
register only the capabilities it needs without creating a shell or supplying `IHostEnvironment`.

```csharp
services.AddFoundationKeycloakAdministration(options =>
{
    options.ServerUrl = "https://identity.example";
    options.Realm = "application";
    options.AdminRealm = "application";
    options.ClientId = "deployment";
    options.ClientSecret = File.ReadAllText(protectedSecretPath);
}, KeycloakAdminFeatures.Realms | KeycloakAdminFeatures.Applications);
```

The caller owns secret-file permissions, credential rotation and process lifetime. Registration
does not grant Keycloak permissions and does not contact Keycloak. Clients acquire a cached
client-credentials token on their first operation. The default transport follows no redirects,
uses no cookies, omits HTTP diagnostic loggers and bounds JSON responses. Production requires
HTTPS. Loopback HTTP requires both the Development environment argument and the explicit
`AllowHttpForLocalDevelopment` setting. Client credentials may authenticate in the application
realm; the historical `AdminRealm` default remains `master` for compatibility, so select it explicitly.

`KeycloakAdminFeatures` is an explicit capability selection. User, enrollment, application, access,
scope and session capabilities expose the existing provider-neutral interfaces. Access also selects
the new `IIdentityEffectiveUserRoleReader` with `GetEffectiveUserRolesAsync`, including composite
realm roles; existing administration interfaces remain unchanged for third-party implementers. Keycloak-only features keep their
provider-specific interfaces. One service provider represents one configured administration identity.

`IKeycloakRealmAdministration` and the separately selected `Realms` shell feature provide typed
configured-realm initialization and settings updates. `CreateAsync(KeycloakRealmSettings)` creates
only the configured realm's settings. Its `KeycloakRealmInitialization` overload adds sanitized
initial clients, scopes/mappers and realm roles, without users, passwords, client secrets or grants.
Provider-generated IDs are rejected before a request. Initialization needs the actual authority to create
a realm; a credential issued by a missing target realm cannot bootstrap itself. `GetAsync` returns
null for an absent realm. `UpdateSettingsAsync` sends supplied non-null settings and preserves
unrelated realm attributes by reading and merging them first. This is a series of API operations,
not an atomic transaction or a deployment engine. Serialize conflicting administrative changes
through the operator's process and verify the resulting state.

Application-created identities, clients, roles, credentials and grants remain operational data.
Deployment tooling defines its own object/field ownership and uses these clients only for
permitted changes. No complete-realm import, automatic deletion or generic migration ledger is
introduced. Runtime clients and deployment clients require separately evaluated Keycloak permissions.

The explicit `Configuration` capability exposes `IKeycloakConfigurationAdministration`. Its new
nullable configuration records support client, scope, mapper and role metadata reads and partial
writes. Omitted scalar fields remain omitted; attribute, mapper configuration and authentication
binding maps merge declared keys with current entries. They expose no user, password or client-secret
fields and perform no implicit deletes. Caller-owned object/field authorization is still required.
Use separate mapper and scope-assignment operations during updates; initial nested declarations
belong only to creation/import. Client logout redirect policy uses Keycloak's
`post.logout.redirect.uris` attribute. Role attributes are multi-valued, unlike client/scope attributes.

Foundation 0.3.14's same explicit `Configuration` capability additionally registers `IKeycloakConfigurationRemoval`.
It keeps deliberate removal separate from omission and leaves the existing configuration interface
unchanged for independent implementations. `RemoveClientScopeAsync(clientId, scopeId, optional)`
removes exactly one selected default or optional assignment, rather than deleting the reusable scope.
To replace an owned bounded membership set, read its current assignments, calculate its exact delta,
authorize each change, then add/remove only those assignments and read back the effective set.

`RemoveClientConfigurationKeysAsync(clientId, attributeKeys, authenticationFlowBindingOverrideKeys)`
removes only the explicitly named client-map keys. Each selection is bounded to 128 unique nonblank
keys of at most 256 characters without controls. Both empty selections do no I/O. The operation reads fresh maps,
preserves unselected entries (including independently configured MFA flows), sends no client scalar,
credential, user or grant fields, and avoids a write when all selected keys are already absent.
An explicit final-key removal sends an empty map. Partial updates still merge supplied map keys;
omitted keys and empty update maps do not authorize removal. The caller owns object/key authorization,
stale-plan rejection, serialization of concurrent administrative changes and read-back/retry; the
native provider API does not make a multi-operation update atomic. Never infer ownership from an
absent declaration or use configuration removal to delete a foreign operator's keys.

## Protected Host settings overlay

The Host can load a protected JSON settings file after `shells.json` and before environment and
command-line configuration. Its startup environment supplies two absolute paths:

- `FOUNDATION_RUNTIME_SETTINGS_FILE` selects the protected operator-owned JSON file.
- `FOUNDATION_RUNTIME_SETTINGS_CONTRACT_FILE` selects the release-owned binding contract.

Both selectors must be supplied together. The binding contract is a separate non-secret file
protected with the release artifacts; the operator's settings file cannot expand its own allowlist.

```json
{
  "schemaVersion": 1,
  "componentId": "application-host",
  "settings": [
    {
      "path": "CShells:Shells:application:Configuration:Foundation:Web:ClientSecret",
      "required": true,
      "kind": "secret"
    },
    {
      "path": "CShells:Shells:application:Configuration:Foundation:Web:Authority",
      "required": true,
      "kind": "https-uri"
    }
  ]
}
```

The protected file is raw nested configuration JSON. Each scalar leaf must match one declared
path. `required` demands a nonblank supplied value. Supported kinds are `secret`, `value`,
`https-origin` and `https-uri`; issuer paths such as `/realms/application` use `https-uri`.
Neither HTTPS kind permits embedded credentials, query parameters or fragments. `https-origin`
requires the exact canonical `scheme://host[:port]` serialization: lowercase scheme/host, no
trailing slash, and no explicit default HTTPS port 443. A nondefault port such as 8443 is preserved.
This matches the literal browser Origin boundary; issuer URLs may carry their declared path.

The loader rejects Nuplane paths, shell activation/identity paths, configuration for nonexistent
shells, unknown contract fields, duplicate or case-duplicate keys and ambiguous key delimiters.
Non-root empty structures are rejected, including forbidden structures containing no scalar leaves.
Files are bounded to one MiB, JSON depth to 32 and leaf counts to 4096. Failures contain no
settings values or raw JSON parse details. The file is loaded once at startup and its values
are held in memory; existing later environment/command-line precedence remains unchanged.
This protects the overlay boundary, rather than claiming to restrict a trusted operator who
can replace the entire runtime configuration or process environment.

Publishers expose `.orbyss-foundation/runtime-capabilities.json` with the
`protected-runtime-settings-v1` capability alongside the loader. Consumers can reject older
images lacking the capability before starting an application. The marker supports discovery;
actual compiled loader tests, image qualification and immutable release identity still establish
compatibility.

Mount secrets only into the process that needs them. Keep bootstrap, deployment and runtime
credentials separate. Neither file mounts nor these clients conceal secrets from a trusted host
administrator, and ordinary application archives must contain no real secret values.
