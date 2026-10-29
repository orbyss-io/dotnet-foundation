# Orbyss.Foundation.Web.HostedPages

An opt-in CShells feature for encoded semantic application pages, typed bootstrap, and explicitly
admitted public runtime/branding assets. Host stays application-neutral. Requires WebDefaults;
its dependency is selected by the feature declaration. Explicit authenticated routes additionally
require the independently selected Foundation BFF-cookie feature.

The typed route/access extension is released from Foundation 0.3.3 and is included in 0.3.9.
Foundation 0.3.2 had only public PagePath hosting. Consumers must select the corresponding qualified
package family and Host image rather than inferring support from an older profile.

This feature holds deployment snapshots, including initial metadata and bootstrap. It does not
perform current resource/publication lookup. Keep revocable business data out of manifests and public
assets. The separately selected [DynamicPages](../Orbyss.Foundation.Web.DynamicPages/README.md)
feature introduced in 0.3.11 supplies request-time consumer resolution and precommit verification;
select its published qualified family after the Release workflow succeeds. Ordinary application IWebShellFeature endpoints are also supported.

## Settings-only consumption

Deploy read-only files below the application content root. Build a Vite library entry exporting
`async mount(element, bootstrap)`, using relative URLs (`base: "./"`) and a manifest. The runtime owns
form behavior and consumes `bootstrap.formReleaseId`; Foundation does not implement a Forms schema,
fetch supplier URLs, or add authoring APIs. No raw branding HTML, JS, CSS or SVG is accepted.

Create a public branding JSON file:

```json
{
  "defaultLocale": "en",
  "theme": "blue",
  "locales": {
    "en": {
      "title": "Estimate your project",
      "purpose": "Complete this form to request an estimate.",
      "logoAlt": "",
      "loadingText": "Loading form",
      "failureText": "The form is unavailable. Please try again later.",
      "noScriptText": "Enable JavaScript to complete this form."
    }
  }
}
```

Build the explicit deployment manifest with repository tooling:

```console
python scripts/create_hosted_manifest.py --root path/to/hosted-pages --vite .vite/manifest.json --branding branding.json --entry src/main.ts --revision release-1 --form-release forms-1
```

The tool prints the exact settings values. Add them under the shell's
`Configuration:Foundation:HostedPages`, alongside `Root: "hosted-pages"` and `PagePath: "/"`.
Enable `Orbyss.Foundation.Web.HostedPages` in that shell's Features map. No consumer middleware or
ConfigureServices registration is needed. The actual Nuplane host must receive the new released
Foundation packages together; a source merge alone does not make these packages available.

The loader fetches the route's typed bootstrap, imports the admitted entry and invokes `mount`.
Private bootstrap uses same-origin credentials; public bootstrap omits credentials. Redirects fail
the bootstrap load rather than following an authentication redirect as application data.
It hides the live loading message on success and announces localized text through an alert on failure.
Initial title, heading and purpose are server-rendered before JavaScript executes. All output contexts
are encoded; styles come from a built-in stylesheet with blue/green/slate theme tokens. Language selection
uses Accept-Language quality values, culture parents and the declared fallback. A logo requires alt text
for every locale and an explicit branding PNG descriptor.

## Admission and serving contract

The exact manifest-byte SHA-256 and current revision must match configuration. Activation validates the
entire current/retained inventory, all static/dynamic Vite imports and CSS/resources, file hashes, MIME/
extension/content, text/locale bounds and budgets. Assets are copied after validation; request handling
does not read the filesystem. Snapshot-hash URL prefixes preserve emitted relative chunk imports. Use
relative CSS font/image URLs too; absolute build-root asset URLs do not follow a shell automatically.

Only runtime JS, CSS, PNG and WOFF2, and authored branding PNG are supported. Runtime JS/CSS is trusted
deployed application code, never user-authored branding. PNG supports non-interlaced 8-bit RGB/RGBA,
validated chunks/CRC/decoded scanlines, at most 8 million pixels, and only IHDR/IDAT/IEND/sRGB/gAMA/pHYs
chunks. Strip unsupported metadata before deployment. WOFF2 receives header checks; runtime font
artifacts remain part of the trusted build. Defaults: 2 MB manifest, 8 MB/file, 32 MB total, 16 revisions,
4096 assets per revision, 32 locales. Total can be raised to 128 MB through settings.

The local source uses PhysicalFileProvider and refuses linked absolute ancestors and relative paths,
traversal, alternate streams, ambiguous filenames and unlisted routes. Deployment directories must be
operator-controlled and read-only to the serving process. File providers are not a sandbox against an
attacker who can rewrite deployment roots concurrently; the separately configured manifest hash and
per-file hashes prevent changed bytes from passing admission.

Public assets are intentionally anonymous. Each shell maps only its admitted snapshots; knowledge of
another shell's intentionally public URL does not make it confidential. Private result downloads and
their authorization/storage remain outside this feature. Unknown or unadmitted references get 404.
Pages and errors are no-store; admitted immutable assets support GET/HEAD, ETags and ranges with accurate
MIME, nosniff and safe inline filenames. No directory browsing or arbitrary remote fetching occurs.

## Revision retention and providers

Changes require a newly admitted manifest and shell recreation. Use `--retain old-manifest.json` to
include retained revisions whose files are still deployed. Foundation never deletes them. Deployment
tooling owns removal and rollback. Keep immutable asset paths distinct between revisions. The current
page refers only to its selected snapshot. Legacy public mode retains revision-only bootstrap hashes.
Explicit routes expose bootstrap only for their selected revision, partitioned by route ID and revision
hash. Retained explicitly public assets remain available; no shared bootstrap alias is registered.

An alternative code provider implements IHostedAssetSource.OpenRead and registers it before feature
composition. The same bounded manifest, hash and content admission applies to provider streams.
It cannot return arbitrary unvalidated response objects. Production blob/remote ingestion providers,
SVG, embedding and arbitrary templates are outside this version.

Run `python tests/validate_hosted_pages.py` after the Release build. The HTTP probe uses real CShells;
the browser fixture is available through the same probe's `--serve` option.

## Authenticated application routes

See [the contract and adoption example](../../docs/hosted-application-pages.md). `HostedPageOptions.SectionName`
owns the configuration section; `FoundationHostedPagesFeature.FeatureName` owns the feature selector.
`Routes` contains at most 64 distinct `HostedPageRouteOptions`, each with a stable `Id`, literal `Path`,
optional admitted `Revision`, and explicit `Page` and `Bootstrap` selections. `HostedPageAccessOptions`
selects `HostedPageAccessMode.Public` or `Authenticated`, optional canonical `Permission` and named
`ResponsePolicy`. Authenticated mode requires a validated BFF session. Bootstrap also requires its
page's permission, and may add one of its own. Private Page plus Public Bootstrap is rejected.

Protected HTML/bootstrap carry `WebResponseMetadata.Private`, use native authorization metadata and
contain no AllowAnonymous metadata. The existing final WebDefaults writer owns all security, cache and
crawler headers, including denials, redirects and errors. Private HTML/bootstrap are no-store and
noindex/nofollow regardless of indexing flags in a selected policy. All hosting endpoints remain
excluded from API description/export. Discovery only consumes its separately admitted public catalog;
never copy private manifest text or data into that catalog.

Omitting Routes retains the public PagePath contract. An explicitly empty/null/sparse Routes selection
is invalid; it cannot fall back to public mode. Do not configure PagePath alongside explicit Routes.
Duplicate case-insensitive IDs/paths, templates/traversal/reserved paths, unknown revisions/policies,
missing access selections, anonymous permissions and absent BFF composition reject activation before
any hosted endpoint is published. Unknown settings reject rather than ignoring a misspelled access key.
Changes require shell recreation; admission and bytes belong to the shell generation.

Assets retain independent, explicit manifest `Visibility: public` admission. `AssetResponsePolicy`
selects their response policy, including public built-in loader/styles. JS/CSS/branding/fonts must contain
no private data. Private downloadable assets are outside this feature. Data APIs must independently
enforce `PermissionPolicyNames.ForPermission(ownerPermission)` plus resource ownership/state rules.
Page permission is neither API admission nor ownership authority.

Run `npm ci --prefix tests/browser`, prepare Playwright engines, then `node tests/browser/hosted-pages.cjs`.
The fixture uses only deterministic local identities and synthetic data. Firefox remains required in
Linux CI/Release; this Windows host uses `--engines=chromium,webkit`. `--serve-private` exposes the same
authenticated source-shell fixture for browser testing. The packaged Host probe adds a test-only cookie
issuer package; that fixture is never part of the runtime family or production Host.
