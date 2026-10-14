# Orbyss.Foundation.OpenApi.Exporter

Orbyss Foundation-managed build tool that composes OpenAPI from a consumer application's validated,
staged feature-package closure without opening a network listener or running shell lifecycle
initializers. Consumer repositories invoke it through `.orbyss-foundation/eng/openapi_pipeline.py`; do not
add this package to feature or application projects.

Contract admission and producer evidence both use the `PackageVersion` assembly metadata generated
from the MSBuild property that sets the tool's NuGet package version. Prerelease identifiers are
preserved; assembly versions and informational source-revision metadata do not define this identity.
The contract must require the exact restored package version.

The validated staged application closure owns `Microsoft.AspNetCore.OpenApi` and
`Microsoft.OpenApi`. The exporter deliberately does not bundle those libraries; it
loads the staged provider assembly and uses its native build-time document-provider
contract. This prevents the tool's own dependency versions from shadowing newer or
older supported runtime closures during feature inspection. A missing staged OpenAPI
implementation remains an error. Shared framework and CShells contract types retain
their normal process identity.

`tests/validate_exporter_runtime_closure.py` restores the exact packaged tool into a
fresh manifest and exports ordinary strict JSON endpoints using public Foundation
0.3.3 and 0.3.4 packages. It preserves public exporter 0.2.5's newer-closure failure,
tests exact producer and metadata rejection, and uses throwing effect-only, hosted
service and endpoint sentinels. Independent publication also executes the newly
published public tool against those closures before declaring the tool release ready.
Immutable legacy Assurance and frozen older OpenAPI dependency tests remain required.

Run `python tests/validate_openapi_exporter.py --packages artifacts/nuget` to restore the packed
release tool into an isolated local manifest and export a real feature endpoint. The validator also
packs a prerelease with an independent informational version, checks exact producer evidence for
both versions, and requires mismatching contracts to fail without producing output or evidence.
