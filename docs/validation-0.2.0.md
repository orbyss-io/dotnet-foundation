# 0.2.0 implementation and validation

Implemented in the requested order: issue 1 response policies, issue 2 typed JSON/canonicalization,
then issue 3 hosted pages/assets. All packages share one 0.2.0 bump.

## Delivered

- Existing WebDefaults now compiles shell-local policies, selects whole endpoint/feature policies,
  owns final headers, and enforces private/error/cookie no-store behavior. BFF and Discovery are integrated.
- New Json and Json.AspNetCore packages provide strict/tolerant typed profiles, bounded reads, controlled
  converters/resolvers, source-generated metadata support, safe HTTP errors and explicit canonicalization.
  Discovery is the representative internal typed-reader migration.
- New Web.HostedPages provides fixed initial semantic HTML, constrained localized branding, public
  bootstrap, hash/content/containment admission, copied immutable assets, Vite graph delivery and retained
  revisions. Manifest-generation tooling and settings-only adoption instructions are included.
- CI runs on Windows and Linux. CI and Release share focused validators and verify actual Nuplane Host
  consumption after packing. Release uses the correct flat Host Dockerfile path.

## Evidence obtained locally

On Windows with SDK 10.0.202:

- Locked solution restore passed.
- Full Release solution build passed with zero warnings/errors.
- All 14 focused validators passed, including existing authentication/analyzer/domain-event/identity checks.
- Additional affected probes passed after final refinements.
- Canonicalization matched 9,995 independent Node/V8 vectors, covering a deterministic 10,000 binary64
  bit-pattern sample (non-finite/zero values excluded) plus Unicode/object vectors.
- Typed legacy ASCII envelope fixtures retain captured numeric strings, answer normalization, ordered
  languages, explicit nulls and references. This is compatibility evidence, not a consumer migration.
- Real two-shell HTTP probes passed for isolation, policy precedence, errors, early rejection, cookies,
  immutable caching, Vite CSS/chunks, retained bootstrap, HEAD/ETag, private/wrong-shell 404, language
  negotiation, traversal, linked roots and malformed manifests.
- Browser verification displayed hostile branding as literal text, loaded static/dynamic imports and
  imported CSS, hid loading status on success, and announced a failing runtime through role=alert.
  The browser reported no script/CSP warnings or errors in these fixtures.
- All 25 NuGet packages packed at 0.2.0 and passed exact package-family metadata validation.
- The actual application-neutral Host loaded the newly packed packages through Nuplane and served a
  hosted page, bootstrap and immutable runtime using only deployment files and shell settings.

## Reproduction

```console
dotnet restore Orbyss.Foundation.slnx --locked-mode --configfile NuGet.config
dotnet build Orbyss.Foundation.slnx -c Release --no-restore
python scripts/validate_foundation.py
dotnet pack Orbyss.Foundation.slnx -c Release --no-build --output artifacts/nuget/0.2.0
python tests/validate_package_metadata.py --packages artifacts/nuget/0.2.0
python scripts/validate_hosted_package_consumption.py --packages artifacts/nuget/0.2.0
```

Node 20+ is required for cross-language vectors. The hosted-page HTTP probe accepts --serve for browser
inspection of disposable success and failure shells. NuGet pack emits informational warnings for
intentionally non-packable Host/probe projects; the expected 25-package inventory is checked separately.

## Deliberate limits

The canonicalizer dependency candidate failed a reproduced subnormal-number check. The user approved
the scoped .NET implementation described in json-canonicalization-decision.md; no failed third-party
canonicalizer was adopted. The numeric suite is regression evidence, not a formal proof.

Policy changes and asset/profile changes require shell recreation. PNG branding is limited to the
documented RGB/RGBA subset. Runtime assets remain trusted deployment code. Public assets provide no
confidentiality; private result authorization/storage remains product-owned. Dynamic endpoint selectors
that are not available during pipeline construction fail closed before their handler runs.

Remote CI, the container build and the complete Release publication workflow have not run for this
working tree. No tags, packages or consumer migration have been published. Local packages are validation
artifacts; a stable release is available only after the complete Release workflow succeeds.
