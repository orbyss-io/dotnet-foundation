# Plans for GitHub issues 1, 2, and 3

The user accepted decisions Q1-Q14 and authorized sequential implementation: issue 1, then issue 2, then issue 3, with one shared version bump. All three are implemented locally for 0.2.0. See [validation evidence](../validation-0.2.0.md). Publication remains a separate Release workflow.

1. [Validated web security policies](issue-1-web-security-policies.md)
2. [Typed JSON and versioned canonicalization](issue-2-typed-json.md)
3. [Hosted pages and safe branding assets](issue-3-hosted-pages-assets.md)

## Delivery order and review

Keep security policies in the existing WebDefaults package. Add `Orbyss.Foundation.Json` for the ASP.NET/CShells-independent core, `Orbyss.Foundation.Json.AspNetCore` for optional HTTP/shell integration, and `Orbyss.Foundation.Web.HostedPages` for hosted pages and admitted public assets. Keep narrow provider interfaces with their implementation package initially. Deployment manifests explicitly identify current and retained revisions; Foundation does not delete revisions automatically.

The user requested sequential implementation even though issues 1 and 2 have independent contracts. Issue 3 integrates both. Each issue includes focused regression evidence, configuration examples and adoption instructions. The canonicalizer assessment failed its candidate; the user explicitly approved the scoped implementation recorded in [the dependency decision](../json-canonicalization-decision.md).

All runtime projects remain directly under `src/`; new repository tooling belongs in `scripts/`. Foundation remains independent of Program Kit and Host remains application-neutral. Existing focused Python validators and .NET probes retain their established `tests/` organization.

Validation includes locked restore, Release build, focused and existing validators, packing, and exact package metadata verification. The package family now contains 25 packages. CI and Release use the same validator script and verify settings-only consumption through the actual packaged Host before publication.

The existing release-path defect is fixed: Release now uses `src/Orbyss.Foundation.Host/Dockerfile`. Its previously missing focused checks now run before publication. CI covers Windows and Linux; local evidence covers Windows.

No consumer migration, bootstrap rerun, approval-state changes, paid agent tests, tagging, or publication is authorized by these plans. Merge is not published availability; a stable tag is available only after the complete Release workflow succeeds.
