# Orbyss.Foundation.Web.HostedPages

An opt-in CShells feature for a fixed, encoded semantic page, typed public bootstrap, and explicitly
admitted public runtime/branding assets. Host stays application-neutral. Requires WebDefaults;
its dependency is selected by the feature declaration.

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

The loader fetches only the typed public bootstrap, imports the admitted entry and invokes `mount`.
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
their authorization/storage remain outside this feature. Unknown/wrong-shell/private references get 404.
Pages and errors are no-store; admitted immutable assets support GET/HEAD, ETags and ranges with accurate
MIME, nosniff and safe inline filenames. No directory browsing or arbitrary remote fetching occurs.

## Revision retention and providers

Changes require a newly admitted manifest and shell recreation. Use `--retain old-manifest.json` to
include retained revisions whose files are still deployed. Foundation never deletes them. Deployment
tooling owns removal and rollback. Keep immutable asset paths distinct between revisions. The current
page refers only to its selected snapshot; retained bootstrap/assets remain available at their hashes.

An alternative code provider implements IHostedAssetSource.OpenRead and registers it before feature
composition. The same bounded manifest, hash and content admission applies to provider streams.
It cannot return arbitrary unvalidated response objects. Production blob/remote ingestion providers,
SVG, embedding and arbitrary templates are outside this version.

Run `python tests/validate_hosted_pages.py` after the Release build. The HTTP probe uses real CShells;
the browser fixture is available through the same probe's `--serve` option.
