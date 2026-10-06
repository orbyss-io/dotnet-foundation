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
retained runtime feed. That generated closure project explicitly disables SDK package pruning:
Nuplane resolves nuspec edges even when the .NET10 SDK would omit a framework-provided dependency.
Ordinary fixture project restores retain SDK defaults; native Host binding must still pass.
Native Host startup also rejects invalid body/header limits before any
application or storage activation. CI uses the repository's pinned SDK. The explicit `--sdk-working-directory`
option records a development substitute without changing that pin.

The Host DLL, adjacent runtime DLLs/deps/runtimeconfig/configuration and native runtime assets are
hashed, copied and verified in an owned snapshot before execution. Full-feed CShells contract
archives remain evidence, while runtime discovery uses the exact contracts already provided by
the Host. Each Nuplane state/extraction/loading path belongs to that qualification run.

The two loopback shells exercise default and deliberately controlled replacement identity readers,
scoped contributions, immutable collection equality/array shape, canonical UTF-8 hashing, typed JSON
admission and error budgets, native handler/fallback dispatch, cookie/BFF writing and shell isolation,
independent PostgreSQL contexts, and a previous generation draining while its owned context remains
alive. Controlled synthetic tickets qualify cookie/reader adaptation; they are not signed OIDC
protocol acceptance. The separate native OIDC conformance test covers that protocol.

External public problem markers returning 200/409 and ignoring declared request admission must
fail before forwarding their oversized bytes. Actual PostgreSQL command expiry preserves the outer
deadline and permits a follow-up query. A controlled per-operation monotonic clock postpones timer
delivery while a fast write reaches its stage cap; a separately owned receipt read checks the actual
database. This test does not replace the deployment's default clock or treat cancellation alone as
proof of rollback.

Fixture routes issue controlled cookies and reload shells. This is retained test code only. No
consumer migration, package publication, paid worker or public-default selection occurs. Connection
credentials go only to the Host's environment, not preserved configuration/log/manifest values.
