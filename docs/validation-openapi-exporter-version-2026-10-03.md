# OpenAPI exporter package version repair: 0.2.3

The published 0.2.2 exporter from commit `8d60cdd55e7fb9056c83d614786667c04ef78bdf`
compared contracts and emitted evidence using a hard-coded `0.9.9-preview.1`.
De Zaaglijst's preserved native failure is recorded in
`.program-kit/evidence/rm01-browser-first-export.txt`, with the installed package and
publisher hashes in `rm01-exporter-version-diagnosis.json`. Its matching 0.2.2
contract was rejected with PKO200 and the native pipeline returned PKO205/exit 2.

The exporter now embeds the MSBuild `PackageVersion` property as assembly metadata.
Both admission and producer evidence read that metadata. This uses the same build
authority as the NuGet package, preserving prerelease identifiers independently of
assembly/file versions and informational source-revision metadata. A contract must
still match the exact tool package version.

## Local validation

Validation used the repository-pinned SDK 10.0.202 on Windows:

- Locked solution restore and Release build passed with zero warnings or errors.
- All focused validators in `scripts/validate_foundation.py` passed.
- Release packing and exact metadata validation passed for all 25 packages at 0.2.3.
- Actual Nuplane Host consumption of packaged hosted pages passed.
- The packed-tool integration validator restored real tool nupkgs into isolated
  manifests using a local-only feed and fresh tool caches. A locked-restored,
  packed feature registered OpenAPI and a real `/test/probe` endpoint.
- Matching contracts for 0.2.3 and 0.2.3-preview.1 exported that endpoint and emitted
  the exact nuspec version in producer evidence, with a verified document SHA-256.
- Mismatching contracts for both versions returned PKO200/exit 2 and produced no
  document or evidence. The prerelease was built by overriding `PackageVersion`
  alone, with an intentionally unrelated `InformationalVersion` of
  `9.8.7+version-probe`.

The validator accepts `--packages artifacts/nuget` to execute the exact release
tool package before publication. CI and Release run that check. Logs, real export
documents, evidence and package hashes are retained under
`artifacts/openapi-exporter/validation-*`; subprocess failures are retained there
as well. The initial fixture package's NU5104 failure was corrected by giving the
fixture a prerelease identity consistent with its prerelease CShells dependencies.

## Release and consumer qualification

These are local candidate checks. A stable 0.2.3 release is available only after
the complete Release workflow, including public NuGet propagation and the Host
container publication, succeeds. The prerelease is a local regression artifact.

Foundation source, tests and release tooling have no dependency on Program Kit.
The Program Kit checker, coordinated pins, consumer contract and approval state
were not modified, and the consumer's failed pipeline evidence was preserved.
After Foundation publication, Program Kit must update its coordinated pins and
qualify its maintained native producer pipeline with released artifacts. Local
Foundation test exports are not consumer qualification receipts.
