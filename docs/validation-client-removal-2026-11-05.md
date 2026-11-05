# Native client removal follow-up

Foundation 0.3.15 corrects the actual provider semantics of the additive Configuration removal API
introduced in 0.3.14. The existing public interfaces and selected capability boundary stay unchanged.
The earlier source, published packages, tag and recording-only evidence remain preserved.

Keycloak merges only supplied client attribute and authentication-flow-binding entries. Omission and
empty maps preserve existing keys; explicit null values remove named entries. The corrected operation
validates and snapshots bounded selections before I/O, reads the latest client, and sends only selected
present keys as null tombstones. It replays no unrelated map entries, scalar policy or credentials.
An already absent selection causes no write. Callers still own authorization, administrative write
serialization and read-back of the multi-operation update.

Both default-scope and optional-scope DELETE endpoints remove the same underlying client/scope
binding regardless of its current category. A category change therefore completes removals before
adding replacement assignments. The Configuration-only removal surface avoids activating broader
portable Scopes administration; the existing Scopes method remains a valid alternative.

The corrected recording model independently merges supplied entries and deletes only explicit null
or empty entries. It reproduced the original native 0.3.14 failure, `Explicit keys survived removal`,
then passed after the fix. Wire assertions require literal named null values and exclude unselected
or absent keys, the other unchanged map and scalar/secret replay. Final-map-key deletion, absent retry,
strict pre-I/O rejection, capability isolation and escaped scope endpoints remain covered.

The retained native real-provider probe passed against immutable Keycloak 26.8.1
`quay.io/keycloak/keycloak:26.8.1@sha256:a78d6b79572168cb25886b80a8b5b8d4ed4390743e3eecb810538683e209dd6b`.
Actual read-back verified first and final selected attribute/binding removal, unrelated attributes
and authentication-flow binding, all typed client scalar/login policy, repeat convergence, unrelated user identity and
password credential IDs. Both default-to-optional and optional-to-default moves, cross-category DELETE
semantics and an unrelated retained assignment passed. This is native disposable-provider evidence;
it does not claim a consumer migration or browser/MFA journey.

Local evidence is retained under ignored `artifacts/client-removal-0315/` and
`artifacts/keycloak-provider/`. The first import fixture failed because the image did not already have
an import directory; its uniquely owned container was removed, and the corrected fixture passed in
17.026 seconds. The original red probe's lingering Windows crash process caused a subsequent file-copy
failure; only that owned process was terminated, and a DLL-based build/probe passed. Current publisher
source bindings were refreshed through their owner after source review. No toolchain or test assertion
was weakened. Official maintenance, image scans and Release remain separate required outcomes.

The real-provider regression is required by Linux CI, dependency qualification and Release. Windows
CI selects explicit recording-only Keycloak scope. The fixture uses a new random loopback port and
unique ownership label, never an active consumer service, and removes its transient realm credentials.
Diagnostics and concise results are retained in native Actions artifacts.

After selecting runtime 0.3.15, the locked solution restore and full Release build passed with zero
warnings/errors (17.35-second build). Exactly 28 lockfiles changed only their runtime project range
from `[0.3.14, )` to `[0.3.15, )`. The rebuilt native recording probe and actual provider regression
passed again; that provider run took 16.292 seconds and removed its owned container. The three
workflow and nine dependency-maintenance unit checks passed. Independent tool versions remain
Build 0.3.3, exporter 0.2.7 and analyzer 0.3.8.

Independent review found the fixture's initial linear cleanup could mask a probe failure if Docker
diagnostics or removal failed. Cleanup now attempts independent owned operations, always attempts
private-file deletion, and preserves the primary outcome plus every cleanup failure in results.
An uncertain create completion still triggers inspection of the unique name; removal requires its
matching ownership label. Private input requests directory/file modes 0700/0600 where supported,
without changing Windows ACLs. Console output and evidence redact the fictional service secret.
Nine focused workflow/cleanup tests cover Docker inspect/log/remove failures, foreign ownership,
primary plus secondary failure retention, timeout after daemon-side create and CI/Release/maintenance
artifact allowlists excluding the private realm even when deletion fails. These replace the original
three-check workflow result; native actual-MFA acceptance remains separately owned by consumers.

Review also caught that plain `docker cp` would make host-private 0700/0600 files root-owned inside
the non-root Linux provider. The bounded in-memory import archive now contains only its import
directory and realm file with those private modes, owned by the selected container's numeric native
user (1000 for this image). Archive copy preserves that ownership; the default native user must
successfully read the imported file before provider qualification continues. No root runtime or broad
permission change is used. The archive regression checks exact members, ownership, modes and bytes.

A confirming run exposed a test comparison defect: the provider returned the same default scope
names in a different order. Structure-only diagnostics identified `DefaultClientScopes` as the only
changed typed field, with ordered equality false and ordinal-sorted sequence equality true. Actual
scope membership and duplicates/cardinality were unchanged. The comparison now canonicalizes map
keys and only `DefaultClientScopes`/`OptionalClientScopes`; every other scalar and array remains exact.
Negative controls reject missing or duplicate scopes, changed redirect-URI ordering, scalar changes
and attribute/binding value changes. No complete provider DTO is written to diagnostics.

The first failed assertion invoked Windows crash reporting and timed out after 120 seconds
(134.742 seconds including fixture cleanup); its original partial streams were unavailable. A native
probe catch now reports fixture exceptions and returns failure directly. Subsequent focused failures
retained their actual assertion and successful owned cleanup; timeout partial-stream preservation is
also covered. The final focused source build, recording controls and real-provider fixture passed.
The final provider result took 15.480 seconds, retained native-user readability, reported no cleanup
failures, removed the owned container and deleted private input. Original failures remain preserved.

Provider contract sources:
[client map updates](https://github.com/keycloak/keycloak/blob/26.8.1/server-spi-private/src/main/java/org/keycloak/models/utils/RepresentationToModel.java),
[attribute null deletion](https://github.com/keycloak/keycloak/blob/26.8.1/model/jpa/src/main/java/org/keycloak/models/jpa/ClientAdapter.java),
[scope DELETE endpoints](https://github.com/keycloak/keycloak/blob/26.8.1/services/src/main/java/org/keycloak/services/resources/admin/ClientResource.java).
