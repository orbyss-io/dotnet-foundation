# Native Host package loading qualification

The Host consumes selected public Nuplane and CShells packages. Its feature
assembly provider intentionally promotes native catalog identities into the Host
lifetime for cross-package contracts. This is not an application isolation or
unloadability promise. Foundation does not patch Nuplane or rename dependency DLLs.

Run `python tests/validate_host_package_loading.py --host <built-or-published-Host.dll>`
against the actual candidate Host. The synthetic fixture restores current selected
CShells and PdfPig into an owned cold cache, repeats LockedMode, then packs and
loads the complete native runtime closure through the Host's real local package
feed and feature discovery. The known one-page PDF exercises PdfPig's separate
assemblies. English and Dutch satellites share their native resource assembly name;
ResourceManager also checks Dutch parent-culture fallback.

A separately built satellite keeps the same simple name and Dutch culture with a
conflicting assembly version. An isolated negative package must fail native
admission with the actual conflict diagnostic. Merely changing bytes while retaining
the same CLR identity is not that publisher conflict contract. Other failure causes
cannot satisfy the case. Original positive packages and Host payload are preserved.

Results retain exact source hashes, public dependency archive identities and their
declared source commits, actual runtime/archive DLL equality, the complete Host
payload before/after, native logs and the independently observed HTTP result. The
test runs in CI, release validation and dependency qualification. Local `--work`
can select an owned shorter Windows artifact directory when the checkout's metadata
paths exceed the host's ordinary path support; this changes no native policy.

The maintained preview.114 Nuplane archives declare source
`f1d1dc2b7c2f22829c8bb8d053e875216f44889a`. Publisher comparison confirms that source
contains the previously established culture-aware fix at
`b8c48b9253209a6c42b7111015a3cc50e17dd13a` (PR 136). The local development proof is
separate from publisher CI, exact image payload/security scans and public release
verification. No local test certifies a consumer application or grants publication.
