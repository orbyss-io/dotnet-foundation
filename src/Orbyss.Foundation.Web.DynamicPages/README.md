# Request-time application pages

`Orbyss.Foundation.Web.DynamicPages` is a separately selected, optional CShells feature.
This feature is introduced in **Foundation 0.3.11**; Foundation 0.3.9 has HostedPages and browser
presentation, but has no DynamicPages package. Select a newly published and qualified
package family after its successful Release workflow before consumer adoption. Foundation Host remains application-neutral.

Use this feature for resource-specific initial HTML and metadata, with an unrestricted
application layout implemented in trusted code. Use HostedPages for deployment-defined
entry documents and admitted public immutable assets. Do not turn resource records into
HostedPages revisions, retained bootstrap or compiled assets.

## Consumer composition

An application feature registers its request-scoped data/authorization owner:

```csharp
services.AddDynamicPageResolver<StallPageResolver>("stalls");
```

Enable `Orbyss.Foundation.Web.DynamicPages` in the shell. Configure this section under
the shell's `Configuration:Foundation:DynamicPages`:

```json
{
  "MaxResponseBytes": 262144,
  "MaxBootstrapBytes": 65536,
  "TimeoutSeconds": 5,
  "Routes": [
    {
      "Id": "stall",
      "Path": "/stalls/{slug}",
      "Resolver": "stalls",
      "Mode": "Public",
      "ResponsePolicy": "default",
      "NoIndex": false,
      "Modules": ["/application/map.mjs"],
      "Styles": ["/application/map.css"]
    },
    {
      "Id": "workspace",
      "Path": "/workspace/{slug}",
      "Resolver": "stalls",
      "Mode": "Authenticated",
      "Permission": "stalls.read",
      "ResponsePolicy": "private"
    }
  ]
}
```

Paths support literal segments and simple required parameters such as `{slug}`; there
are no catchalls, optional/default parameters or protocol-route aliases. At most 64
routes are admitted. Unknown settings, unresolved resolver keys, missing access modes,
public permissions, absent native BFF composition and unknown policies fail activation.
Authenticated routes use the native BFF session and canonical permission policy; they
do not replace authentication or grant roles to users. APIs/downloads independently
enforce their permissions, tenancy, resource ownership and state rules.

The consumer implements `IDynamicPageResolver`. `ResolveAsync` reads current publication
and resource authorization and returns `DynamicPageResolution(Admitted, version, model)`
only with an authorized, coherent projection. Other outcomes are NotFound, Forbidden
and Unavailable. NotFound may deliberately conceal an ineligible resource's existence.
An absent/malformed result or exception yields a safe unavailable response; no old
model is retained or used as a fallback.

The request supplies the native principal, bounded route values, actual application
prefix and native request-localization Locale. The consumer owns translated resource
queries and may use locale parameters when its URL design calls for them.

`VerifyAsync` is mandatory and is called on the same scoped resolver **after rendering,
before any resource bytes or metadata headers are committed**, for GET and HEAD alike.
It must reread authoritative eligibility and check the exact version supplied by
ResolveAsync. A changed publication/content/authorization version must reject; perform
a fresh request rather than returning a stale model. Database reads and time-sensitive
publication windows belong to that consumer operation. A server-memory or deployment
snapshot alone cannot establish current eligibility.

The verification read is the admission decision point. A change committed before that
read must be reflected. Bytes already committed or in flight cannot be recalled. If the
product needs stronger ordering against concurrent publication writes, its provider must
coordinate reads/writes and response admission explicitly; this library supplies no
cross-database transaction or network-revocation guarantee.

## Rendering and assets

`DynamicPageDocument` supplies plain Title, Description, Locale, Heading, Text, optional
shell-local CanonicalPath and optional JsonElement Bootstrap. Text fields are bounded
to 16,384 characters; JSON has finite bytes/depth and the complete response has its own
byte budget. The default renderer encodes every text/attribute sink and places bootstrap
in inert `application/json`, escaping script-end delimiters. It renders no arbitrary
HTML, CSS, executable data, remote URL or request-derived host.

The initial title, description and semantic content are present before JavaScript.
`#foundation-application` is a flexible application mount with no fixed form or desktop
width. The consumer's responsive layout/map application owns its styling, accessibility
and actual map behavior. Modules/styles must be trusted separately served shell-local
deployment assets; this feature neither reads arbitrary filesystem paths nor fetches
remote assets. Native URL generation preserves shell/outer prefixes. Hash-addressed
public assets may use HostedPages admission or another explicitly owned ASP.NET asset
endpoint; never place private or publication-dependent content in their bytes.

The public `/_foundation/dynamic-pages/v1/page.mjs` utility exports `readPageData()`.
It parses the inert element; it grants no authorization and makes no freshness claim.
The application module can instead read and parse that element directly.

For a different document structure, register `IDynamicPageRenderer` through the owning
feature. It receives an immutable admitted model, not a response, provider exception or
request. It must encode text and attributes and use safe DOM APIs. The complete output
budget and post-render verification still apply. A code renderer is trusted application
code, not an HTML sandbox for tenants or user-authored templates.

The cooperative deadline covers lookup, rendering and verification, and is linked to
request cancellation. Resolver I/O must propagate its token. A synchronous renderer or
consumer ignoring cancellation cannot be safely preempted in-process; it remains owned
trusted code. Native request cancellation is never relabelled as a provider outage.

## Caches, headers and browser state

Dynamic endpoints explicitly disable native output caching and never opt into immutable
asset classification. HTML and errors are no-store. ETags/ranges/304 shortcuts are not
implemented for resource pages: conditional requests still resolve and verify current
eligibility. WebDefaults remains the single final writer for security/cache/crawler
policy, including denials and errors. NoIndex false only permits indexing when the
selected named policy also permits it. Private responses remain noindex/nofollow.

Operators must exclude dynamic HTML/data/media from CDN and service-worker caches; a
proxy or worker deliberately ignoring no-store is outside this feature's enforcement.
Do not configure an upstream cache that bypasses native endpoint admission. Changing
publication eligibility requires no redeployment. Remove any previously deployed
sensitive snapshot and remediate external caches explicitly; hashes do not revoke it.

Fresh HTML does not solve restored browser DOM. Applications must hide private or
revocable resource roots on page hide and recheck **resource publication as well as
session state** on restore/visibility before showing data. Presentation's session guard
alone does not establish current stall eligibility. Native BFF session proof also does
not establish immediate provider revocation or current domain-role assignments.

Configure a named CSP for the selected map library and actual tile/image/connect
origins. Geolocation needs an appropriate Permissions-Policy, a secure browser context
and user permission. Keep framing, object and base restrictions; no unsafe-inline/eval
exception is introduced by this feature. Private media uses independently authorized,
no-store endpoints, not retained public image URLs. Discovery/sitemaps must obtain their
own current public projection; this package exports no resource catalog.

## Qualification

After locked restore and Release build, run `python tests/validate_dynamic_pages.py`.
The native fixture covers public-only and BFF compositions, independent permissions,
shell prefixes/isolation, scoped resolvers, publication changes without redeploying,
post-render races, conditional/HEAD requests, encoding, failures, limits and custom layout.
`tests/browser/dynamic-pages.cjs` exercises actual execution, metadata, bootstrap and
mobile layout under the selected CSP. Package/Host consumption and published-profile
adoption remain separate evidence. Tests use fictional resources and local identities.
