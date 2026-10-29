# Dynamic page and browser recovery adoption

Introduced in Foundation 0.3.11. Public availability requires its successful Release workflow.
The selected Foundation 0.3.9 baseline remains valid and unchanged. It already contains
Web.Presentation, IProblemPageRenderer, IAuthenticationErrorWriter, native BFF controls,
and public/authenticated literal HostedPages routes. It has no dynamic-page resolver.

Use [DynamicPages](../src/Orbyss.Foundation.Web.DynamicPages/README.md) for current resource
HTML/metadata and [Presentation](../src/Orbyss.Foundation.Web.Presentation/README.md) for
default configurable recovery. Consumers can instead author application IWebShellFeature
endpoints; Razor/MVC layouts require actual shell, view discovery and package qualification.
The Host must remain neutral and Foundation must not depend on Program Kit.

## Ownership

Foundation owns native request admission mechanisms, bounded safe defaults, code renderer
seams, native BFF reuse and response policies. Consumers own records, publication windows,
moderation, tenancy/ownership predicates, permission assignments, projections, translations,
branding, responsive layout and map libraries. A renderer never substitutes for eligibility.

Publication reads and version checks must come from the consumer's authoritative state.
Unknown state rejects disclosure. Every independent API/media/discovery path has the same
consumer obligation. Neither session authentication nor an endpoint permission establishes
current publication eligibility. Previously public bytes cannot be recalled from third parties.

## Toolkit follow-through

Toolkit owners should expose separately selected dynamic-page composition and code-owned
resolver registration without converting records into deployable manifests. Preserve existing
HostedPages configurations and authored frontend layouts. Public/authenticated frontend
selection needs explicit toolkit support: the current Program Kit frontend route generator
emits authenticated routes even though Foundation supports public entries. Do not infer
consumer adoption or modify a frozen dependency profile from this Foundation source change.

Knowledge should distinguish IProblemPageRenderer (released, errors only), the new recovery
layout renderer, legacy custom authentication writers and settings-only presentation. Refresh
versioned package settings/feature/XML/README knowledge and qualify a new toolkit profile only
after actual publisher availability. Translate all chosen copy keys to avoid mixed fallback text.

## Focused evidence and release requirements

Run locked restore and Release solution build; inspect compiled settings/feature metadata.
Run dynamic-page, problem-details and native BFF/presentation probes and relevant existing
HostedPages/WebDefaults regressions. Browser fixtures cover actual CSP, execution/encoding,
responsive layout, localization, focus and native logout recovery. Retain Firefox in affected
Linux CI scenarios; use Chromium/WebKit on the known limited local Windows host.

Build the runtime package family with the new DynamicPages package, validate its descriptors
and cold installed compilation/runtime, and exercise the actual neutral Host loading consumer
resolver and presentation packages. Keep new browser fixtures in the affected CI/Release
scenarios. Package count checks intentionally change from 30 to 31.

The maintained publication workflow must validate the exact candidate package/Host/image
payload, relevant platform/browser checks and security scans, then publish only with normal
human authority. These source tests do not qualify Linux, provider logout for a production
identity provider, a remote session-cache backend, a consumer application or public availability.
No tag, package publication, consumer lock update or Program Kit qualification follows merely
from implementing this source. Preserve the existing dependency maintenance/release procedure.
