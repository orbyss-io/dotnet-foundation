# Orbyss.Foundation.WebDefaults

`Foundation:Web:SupportedLocales` replaces the fallback locale list when explicitly configured.
Omission retains `["en"]`; an explicit selection must be a non-empty indexed array of distinct,
non-empty culture names and contain `DefaultLocale` (which itself defaults to `en`). Set both
`DefaultLocale: "nl"` and `SupportedLocales: ["nl", "de"]` to enable only Dutch and German.
Empty, null and malformed selections fail validation. Configuration-provider precedence applies
before binding; recreate the shell to apply changes.

An optional CShells middleware feature providing Orbyss Foundation's default correlation identifier,
browser-security response headers, localization, and production HSTS behavior. Consumers can
deactivate this feature and activate their own equivalent middleware feature.

## Named response policies

Select the feature and configure the shell's `Configuration:Foundation:Web:ResponsePolicies`:

```json
{
  "DefaultPolicy": "default",
  "Policies": {
    "default": {
      "ContentSecurityPolicy": "default-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'self'",
      "ReferrerPolicy": "same-origin",
      "PermissionsPolicy": "camera=(), microphone=(), geolocation=()",
      "AllowIndexing": false
    }
  },
  "FeaturePolicies": { "My.PublicPage": "default" }
}
```

The built-in `default`, `private`, and `public-asset` policies preserve framing and object restrictions.
Configuration is compiled once per shell activation. Restart/recreate the shell to apply changes.
Invalid settings fail activation under the Host's existing activation-failure policy; no permissive fallback is used.
Application settings are inherited through CShells configuration, then shell settings override them.

Features attach `WebResponseMetadata(Feature: "My.PublicPage")` to their route group. An endpoint may
select a complete policy with `WebResponseMetadata(Policy: "public-asset", ImmutablePublicAsset: true)`.
Precedence is endpoint selection, feature selection, shell default, Foundation default. Conflicting
endpoint selections or feature owners are rejected. This is explicit endpoint ownership, not reflection
over handler types. Mark private responses with `Private: true` regardless of authentication metadata.
Mapped selectors are checked when endpoint sources are available during pipeline construction. A selector
introduced later that cannot resolve fails closed with `web_policy_invalid`, default security headers and
no-store; its handler never runs.

One response-start writer owns CSP, framing, nosniff, referrer, Permissions-Policy and cache headers.
Private responses, errors, unclassified/static middleware responses and responses with cookies are
`no-store`. Only explicitly admitted immutable public GET/HEAD resources with status 200/206/304 get
public caching. Resource `NoIndex` and policy denial override indexing permission. Discovery retains
its standalone protection when this feature is not selected. Request authorization and same-origin
admission remain independent. Shell policies cover requests inside that shell pipeline, not proxy or
pre-shell failures. Embedding, unsafe-inline and unsafe-eval policies are not supported.

Run `python tests/validate_web_policies.py` after the Release solution build. Its real CShells probe
exercises two isolated policy catalogs, error response clearing, endpoint overrides, private responses,
public caching and header conflicts.
