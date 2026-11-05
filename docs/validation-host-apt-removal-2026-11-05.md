# Host APT security repair

Foundation PR 29's original [CI run](https://github.com/orbyss-io/dotnet-foundation/actions/runs/41951853435)
passed Windows and failed Linux's actual image security gate on both architectures. Trivy 0.76.1
reported High CVE-2026-109518 in Ubuntu 24.04's APT 2.8.3 and libapt-pkg6.0t64 2.8.3. Earlier
maintenance scans used older advisory coverage and do not replace this failed result. No package,
tag or image was published from this candidate.

[Canonical's notice](https://ubuntu.com/security/notices/USN-8996-1) identifies Noble's fixed package
version as 2.8.4ubuntu0.1. A narrowly pinned HTTPS/zero-retry update was built successfully on both
architectures, with signed archive verification and exact installed-version assertions. Actual
publication metadata confirmed both packages at that version on amd64 and arm64. Fresh unchanged
Trivy scans still marked that installed version affected because the advisory had no FixedVersion.
The full source attempt, installed-version proof, original and fixed-pair failures remain ignored
diagnostics. That attempt and its proposed Ubuntu maintenance backend were not retained in source.

The runtime Host has no advertised or exercised APT requirement. Its supported package loading
uses Nuplane; device preparation's apt-get instructions concern the developer's host, not this
image. Native metadata classifies both APT packages as non-Essential and non-Protected. Plain
offline dpkg removal/purge passed dependency checks without force or essential-package overrides.

Both Host Dockerfiles now purge only apt and libapt-pkg6.0t64 offline and assert that their native
package records and manager executables are absent. The immutable base, RID, .NET runtime, UID 1654,
shell and filesystem helpers remain selected. No new repository, package input, scanner flag,
severity filter, ignore entry or VEX waiver was introduced. The SDK build stage is unchanged.

Focused reproduction used the same Trivy 0.76.1 release and unchanged security-owner arguments,
with isolated fresh databases. The original immutable base failed on both platforms; the removal
candidate passed both with no reported High/Critical findings. The same DB revision was used for
the comparison. Runtime checks on amd64 and arm64 (the latter emulated on amd64) preserved all 95
other installed package versions exactly, UID 1654, sh/id/chown/chmod/dpkg, private-file operations,
writable package storage and actual neutral Host HTTP recovery/security headers. Owned containers
were removed. This is focused development proof; official maintenance, required CI and Release must
still qualify the final source and publication bytes.

Ten focused workflow/cleanup/safety contracts and the nine unchanged dependency-maintenance tests
passed. Raw manifests, package inventories, version assertions, DB metadata, tool identities,
both-platform findings and native HTTP logs remain under ignored `artifacts/host-security-fix/`
and `artifacts/client-removal-0315/pr-ci-41951853435-failure/`. The runtime family remains 0.3.15;
independent tool versions and every published version remain unchanged.
