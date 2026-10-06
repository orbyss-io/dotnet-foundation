# Independent packaged contract fixture

This disposable conformance fixture has separate Core, runtime implementation, API and PostgreSQL
packages. Every reference to Foundation and the other fixture packages is an exact NuGet
PackageReference. Its local props/targets isolate it from Foundation source build references.

`tests/validate_contract_package_consumption.py --packages <private-feed> --version <exact-version>
--host <built-Foundation-Host.dll> --postgres-connection <disposable-target>` builds the packages in a
fresh retained artifact directory, performs locked restores, and loads them through actual Nuplane
and Foundation Host. Repository-configured CShells/Nuplane preview sources and source mappings are
preserved alongside the private candidate feed. The entire candidate runtime dependency closure is
restored into a fresh run-owned cache, and its external package bytes are hashed and copied into the
retained runtime feed. Native Host startup also rejects invalid body/header limits before any
application or storage activation. CI uses the repository's pinned SDK. The explicit `--sdk-working-directory`
option records a development substitute without changing that pin.

The two loopback shells exercise default and deliberately controlled replacement identity readers,
scoped contributions, immutable collection equality/array shape, canonical UTF-8 hashing, typed JSON
admission and error budgets, native handler/fallback dispatch, cookie/BFF writing and shell isolation,
independent PostgreSQL contexts, and a previous generation draining while its owned context remains
alive. Controlled synthetic tickets qualify cookie/reader adaptation; they are not signed OIDC
protocol acceptance. The separate native OIDC conformance test covers that protocol.

Fixture routes issue controlled cookies and reload shells. This is retained test code only. No
consumer migration, package publication, paid worker or public-default selection occurs. Connection
credentials go only to the Host's environment, not preserved configuration/log/manifest values.
