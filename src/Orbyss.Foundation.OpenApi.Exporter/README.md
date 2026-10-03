# Orbyss.Foundation.OpenApi.Exporter

Orbyss Foundation-managed build tool that composes OpenAPI from a consumer application's validated,
staged feature-package closure without opening a network listener or running shell lifecycle
initializers. Consumer repositories invoke it through `.orbyss-foundation/eng/openapi_pipeline.py`; do not
add this package to feature or application projects.

Contract admission and producer evidence both use the `PackageVersion` assembly metadata generated
from the MSBuild property that sets the tool's NuGet package version. Prerelease identifiers are
preserved; assembly versions and informational source-revision metadata do not define this identity.
The contract must require the exact restored package version.

Run `python tests/validate_openapi_exporter.py --packages artifacts/nuget` to restore the packed
release tool into an isolated local manifest and export a real feature endpoint. The validator also
packs a prerelease with an independent informational version, checks exact producer evidence for
both versions, and requires mismatching contracts to fail without producing output or evidence.
