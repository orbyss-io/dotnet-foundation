# Automatic dependency updates

Run this repository's **Update dependencies** workflow before preparing a runtime
or tool release, or trigger it manually for an upstream update. It also runs weekly.
It has no dependency on Program Kit or another consumer repository.

The job upgrades active SDKs, runtime/test/tool packages, CI actions and both Host
Dockerfile digests. It refreshes NuGet locks and vendor settings metadata, builds
and packs, runs all deterministic validators, qualifies the portable Host with real
package consumption and PostgreSQL, and scans both candidate image platforms.
Only a passing job opens an update PR. Failed lookups or checks stop the update and
preserve diagnostics; fix the failure and rerun. No dependency review forms are needed.

For local release preparation, run
`python scripts/update_dependencies.py --upgrade --prepare-source` with Docker,
Node, Python and the selected .NET SDK available. This refreshes source inputs and
locks; it grants no qualification. Review and commit those inputs, then run
`python scripts/update_dependencies.py --export-knowledge` from the clean candidate.
The latter builds, packages and qualifies the exact commit, exports versioned
`foundation-knowledge-candidate.json` beside its payload, and refuses dirty source.
The maintained workflow follows these stages before scanning both platforms and
pushing an update PR. Failed checks retain the unpushed candidate and diagnostics.
The existing unflagged/local `--upgrade` commands remain available for dependency
checks; release selection additionally requires the clean candidate export and scans.
Add new external inputs to maintenance-policy.json in the same change. Linux CI
supplies symlink support needed by HostedPages; Windows hosts without that
privilege cannot pass its link leg.

Runtime and independently released tools retain their own versions. Tagged Release
workflows publish exact package schemas, descriptors, interfaces and frozen source
guidance as release knowledge, along with the runtime Host digest. Consumers can
qualify and adopt that release without guessing which older documentation applies.
Published versions remain immutable; update jobs open PRs and do not publish or
rewrite existing tags.

Sources: [.NET image maintenance](https://github.com/dotnet/dotnet-docker/blob/main/documentation/vulnerability-reporting.md),
[Dependabot configuration](https://docs.github.com/en/code-security/reference/supply-chain-security/dependabot-options-reference),
[Trivy scanning](https://trivy.dev/docs/dev/guide/scanner/vulnerability/).
