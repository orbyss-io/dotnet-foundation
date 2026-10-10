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
public caching. Resource `NoIndex` / `NoFollow` and policy `AllowIndexing` / `AllowFollowing` denial override permission.
Private responses and errors always emit `noindex, nofollow`. `AllowFollowing` defaults to true for
compatibility; `AllowIndexing` defaults to false. The final writer replaces authored crawler headers
and removes them when both permissions are admitted. Use response metadata for application restrictions. Discovery retains
its standalone protection when this feature is not selected. Request authorization and same-origin
admission remain independent. Shell policies cover requests inside that shell pipeline, not proxy or
pre-shell failures. Embedding, unsafe-inline and unsafe-eval policies are not supported.

Run `python tests/validate_web_policies.py` after the Release solution build. Its real CShells probe
exercises two isolated policy catalogs, error response clearing, endpoint overrides, private responses,
public caching and header conflicts.

## Response-local editor styles

`WebResponsePolicyOptions.AllowStyleNonce` defaults to false. Admit it only in a named policy for
an editor that requires generated style elements. After application session/permission admission,
call `context.TryGetStyleNonce(out var nonce)`. False means the capability is denied or the Foundation
writer is absent/the response has already started. Do not render nonce-dependent editor controls when denied.
A successful call returns one cryptographic 32-byte value for that response; repeated calls return the
same value. Encode it as the style element's `nonce` attribute, or as a public bootstrap attribute used
only to construct that response's editor styles. Never log it or configure reusable nonce bytes.

The final writer adds the value to effective `style-src-elem`, `style-src`, or a new `style-src`
using `default-src` fallback. Explicit effective `'none'` remains denial, including a stricter
`style-src-elem`. No script directive or framing/object/base restriction is changed. Static nonce sources,
mixed `'none'` source lists, unsafe-inline and unsafe-eval are rejected at shell activation. A late
endpoint policy change is rechecked before the final write; later denial or an error discards the nonce.
Responses that issued a nonce are always no-store, even when a late policy denies its CSP contribution
and the resource is marked as an immutable public asset. Markup may already contain the issued value.

For explicit adoption, replace consumer nonce generation and CSP rewrite middleware with this request
seam, select a policy with `AllowStyleNonce: true`, and keep private route metadata. Preserve product
session/permission admission and editor denial tests. Recreate the shell after policy changes. Public
Foundation 0.3.1 remains unchanged; this additional API requires the explicitly qualified development
candidate until its publication and exact composition qualification gates complete.
