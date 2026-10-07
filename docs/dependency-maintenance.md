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

Use `python scripts/update_dependencies.py --upgrade` locally with Docker, Node,
Python and the selected .NET SDK available. Add new external inputs to
maintenance-policy.json in the same change. Linux CI supplies symlink support
needed by HostedPages; Windows hosts without that privilege cannot pass its link leg.

Runtime and independently released tools retain their own versions. Tagged Release
workflows publish exact package schemas, descriptors, interfaces and frozen source
guidance as release knowledge, along with the runtime Host digest. Consumers can
qualify and adopt that release without guessing which older documentation applies.
Published versions remain immutable; update jobs open PRs and do not publish or
rewrite existing tags.

Sources: [.NET image maintenance](https://github.com/dotnet/dotnet-docker/blob/main/documentation/vulnerability-reporting.md),
[Dependabot configuration](https://docs.github.com/en/code-security/reference/supply-chain-security/dependabot-options-reference),
[Trivy scanning](https://trivy.dev/docs/dev/guide/scanner/vulnerability/).
