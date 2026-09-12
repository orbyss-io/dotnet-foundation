# Issue 3: hosted pages and safe branding assets

Issue: https://github.com/orbyss-io/dotnet-foundation/issues/3

Status: implemented locally as the third issue in the shared 0.2.0 bump. See [validation evidence](../validation-0.2.0.md). Consumer migration and publication have not been performed.

## Outcome

An independently selected Foundation feature renders initial semantic HTML from a narrow public projection and serves only explicitly admitted public assets. A supported settings-only configuration works with the external Nuplane/CShells Host.

## Agreed decisions

- Branding changes create immutable published revisions. Use a fixed, encoded server template for the selected revision, rather than client-only initial content.
- Support one constrained semantic layout initially, with localized title/purpose text, accessible loading/failure content, and constrained branding tokens. Consumer-authored templates are outside this delivery.
- Author-supplied branding is raster-only initially. Trusted application JavaScript/CSS and other admitted runtime resources form a separate deployment-artifact category; this does not permit authored raw HTML/JS/CSS.
- Explicitly published branding is anonymously public through its owning shell. Cross-shell/tenant isolation concerns lookup and admission, not secrecy of another tenant's intentionally public URL. Confidential assets require independent authorized delivery.
- Use issue 1's response-policy contracts and issue 2's typed readers. Snapshot the validated configuration, manifest, and assets for a shell lifetime; avoid per-request manifest reads.

## Current implementation

Host currently composes Nuplane and CShells and calls `MapShells`; it contains no hosted-page implementation. Discovery provides useful explicit-manifest, hash, bounds, and path-validation patterns, but its broader HTML/CSS/JS/SVG document contract is not an appropriate branding contract.

The existing CSP is same-origin and forbids framing. Rendering must work without arbitrary inline scripts/styles. External packages discovered at runtime cannot assume that the Host's build-time static-assets manifest contains their assets.

## Agreed package boundary

Add `Orbyss.Foundation.Web.HostedPages` directly under `src/`, with narrow provider contracts and a default local configuration/manifest implementation. Keep Host neutral and avoid a provider-abstractions package until a concrete independent consumer requires it.

## Implementation sequence

1. Record the A/B/C tradeoff: client-only static shell does not provide required initial semantic content; fixed encoded template with typed bootstrap is selected; full rendering framework is unjustified for the agreed layout. Document any external governance review needed without changing Program Kit state.
2. Define the public page/branding DTO, locale fallback and multilingual alt text, constrained tokens, release identity, opaque asset IDs, admitted deployment manifest, and narrow provider interfaces. Exclude supplier/private fields and result data by contract.
3. Implement typed settings binding and activation-time admission of a complete snapshot. Check manifest version/schema, duplicate identities, required entry points, configuration revision agreement, recursive Vite imports/CSS/chunks, and every referenced resource.
4. Validate read-only-root containment, linked ancestors/reparse points, extension/MIME/content agreement, byte/count and decoded-image bounds, hashes, and admitted routes. Define a coherent immutable read strategy that avoids serving files changed after validation. Never resolve browser-supplied filesystem paths or fetch arbitrary remote URLs.
5. Render context-encoded fixed markup and public bootstrap, with initial title/purpose content and accessible loading/failure/no-JavaScript behavior. Use external admitted scripts/styles and verify CSP compatibility in a browser. Select supported language using framework facilities and documented deterministic fallback.
6. Serve explicitly admitted assets with correct MIME, safe names/disposition, GET/HEAD and conditional behavior, and issue 1's cache/security policies. Unlisted, private, wrong-shell, and malformed identities must not expose data. Private result downloads remain independently authorized and outside the public catalog.
7. Add external-host consumption examples using settings and narrowly scoped provider overrides, regression fixtures, HTTP/browser probes, and validator wiring. Document revision activation, rollback, retention, missing assets, and limits.

## Regression evidence

- Hostile branding in HTML text, attributes, URLs, JSON/bootstrap, and constrained styling inputs; no executable authored markup or supplier/private projection fields.
- Initial semantic HTML, localization/quality-value handling and fallback, multilingual alt text, accessible loading and failure states, no-JavaScript behavior, and CSP-compatible runtime loading.
- Imported Vite CSS, recursive/static/dynamic chunks, missing entries, malformed manifests, hash/MIME/content mismatch, bounds, stale configuration, and incoherent revisions.
- Traversal and encoded paths, linked roots/ancestors/leaves, wrong-shell/tenant resolution, unlisted/private resource access, and mutation after admission. Exercise platform-specific link behavior on Windows and Linux.
- Public assets cannot expose result data; accurate MIME/disposition; successful immutable caching, conditional requests, error `no-store`, and independent authorization for private downloads.
- Real external-host feature loading and shell path prefixes; settings-only consumption; narrowly scoped provider override behavior.

## Completion and limits

Run locked restore, Release build, focused and existing validators, and package validation. Update package-family expectations for reviewed additions. Deliver adoption instructions and regression evidence. Do not patch PriceCalculator, rerun its bootstrap, modify approval state, expand authoring/embedding, add a production blob provider or remote ingestion, run paid agent tests, tag, or publish.

## Agreed revision lifecycle

Each deployment manifest explicitly lists the current and retained revisions. Foundation serves admitted retained revisions and never deletes them automatically. Deployment tooling owns retention and removal; missing required revisions fail activation rather than silently mixing snapshots. This adds no live-update or storage-management service.
